# Ordering Bounded Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder `Order`/`OrderBuilder` stub with a real Ordering bounded context (Domain → Application → Infrastructure → API) that places orders and reserves stock from Inventoring purely through domain events.

**Architecture:** `Order` aggregate raises `OrderCreated`; a new Inventoring handler reacts to it and reserves stock per line, raising `StockReserved`/`StockReservationFailed` (both now correlated with an `OrderId`); Ordering handlers react to those to confirm or cancel the order; a compensation handler releases any already-reserved lines when an order is cancelled. All cross-context communication is event-only — no direct service calls between Ordering and Inventoring.

**Tech Stack:** .NET 10 minimal APIs, MSTest + Moq, Dapper/PostgreSQL (raw SQL, no EF), event sourcing via `AggregateRoot`/`IAggregateRepository<T>`, reflection-based DI registration of `IRequestHandler<,>`/`IDomainEventHandler<>`.

**Spec:** `docs/superpowers/specs/2026-09-25-ordering-bounded-context-design.md`

## Global Constraints

- No direct service-to-service calls between Ordering and Inventoring — only domain events (spec, cross-context event chain).
- No snapshotting for `Order` in v1 — don't override `CreateSnapshot()` (spec, Domain model).
- Cancelling a `Confirmed` order is out of scope — `Cancel()` is only valid from `Created` (spec, Domain model / Known gaps).
- Coverage for `Core`/`Domain`/`Application` must stay at or above the existing 75% threshold (`./scripts/run-unit-tests.sh`).
- Follow existing conventions exactly: MSTest + Moq for unit tests, Dapper raw SQL (no EF) for read models, reflection-scanned DI (no manual registration of handlers, only of services/repositories).

## Review Focus

- An order line references a SKU with no inventory record at all — `OrderReservationHandler` must still raise a `StockReservationFailed` so the order gets cancelled instead of hanging in `Created` forever (Task 3).
- The same `StockReserved` event for a line is delivered twice (retry/at-least-once delivery) — `Order.MarkLineReserved` must be idempotent and must not raise `OrderConfirmed` a second time (Task 1).
- A multi-line order where some lines reserve successfully before one fails — `ReservationCompensationHandler` must release only the lines that actually reserved and must not throw when releasing a line that was never reserved (Task 3).
- `PlaceOrderCommand` contains a line with quantity ≤ 0, or a SKU that resolves to no product — placement must fail atomically before any stock reservation is attempted, not partially process other lines (Task 5).
- The existing manual `POST /inventory/{productId}/reserve` endpoint behavior must be unchanged after `Inventory.ReserveStock`'s signature changes — insufficient stock with no `orderId` must still throw `InvalidOperationException` (translated to `Result.Failure` by the existing handler), not silently succeed or only publish an event (Task 2).

---

### Task 1: `Order` aggregate, value objects, and events

**Files:**
- Create: `src/Domain/Ordering/OrderStatus.cs`
- Create: `src/Domain/Ordering/OrderLine.cs`
- Create: `src/Domain/Ordering/Events/OrderCreated.cs`
- Create: `src/Domain/Ordering/Events/OrderLineReserved.cs`
- Create: `src/Domain/Ordering/Events/OrderConfirmed.cs`
- Create: `src/Domain/Ordering/Events/OrderCancelled.cs`
- Modify (full rewrite): `src/Domain/Ordering/Order.cs`
- Delete: `src/Domain/Ordering/OrderCreated.cs` (replaced by `Events/OrderCreated.cs`)
- Delete: `src/Domain/Ordering/OrderRenamed.cs` (not part of this design)
- Delete: `src/Domain/Ordering/OrderBuilder.cs` (unused placeholder — no test or handler references it; tests below build `Order` via `Order.Place()` directly, matching how `InventoryTests.cs` uses `Inventory.Create()` directly)
- Test: `tests/UnitTests/Domain/Ordering/OrderTests.cs`

**Interfaces:**
- Produces: `OrderLine.Create(SKU sku, int quantity, Price unitPrice) : Result<OrderLine>`; `OrderStatus` enum (`Created`, `Confirmed`, `Cancelled`); `Order.Place(Guid customerId, IEnumerable<OrderLine> lines) : Result<Order>`; `Order.MarkLineReserved(SKU sku) : Result`; `Order.Cancel(string reason) : Result`; `Order.CustomerId`, `Order.Status`, `Order.Lines` (`IReadOnlyList<OrderLine>`). Events: `OrderCreated(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderLine> Lines)`, `OrderLineReserved(Guid OrderId, SKU Sku)`, `OrderConfirmed(Guid OrderId)`, `OrderCancelled(Guid OrderId, string Reason, IReadOnlyList<OrderLine> Lines)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/Domain/Ordering/OrderTests.cs`:

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Domain.Ordering;

[TestClass]
public class OrderTests
{
    private static OrderLine Line(string skuPrefix = "TS", int quantity = 1) =>
        OrderLine
            .Create(
                SKU.Create(SkuGenerator.Generate(skuPrefix)).Value!,
                quantity,
                Price.Create(10m).Value!
            )
            .Value!;

    [TestMethod]
    public void Place_ShouldRaiseOrderCreatedEvent()
    {
        var customerId = Guid.NewGuid();
        var line = Line();

        var result = Order.Place(customerId, [line]);

        Assert.IsTrue(result.IsSuccess);
        var order = result.Value!;
        var @event = order.UncommittedEvents.OfType<OrderCreated>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(order.Id, @event.OrderId);
        Assert.AreEqual(customerId, @event.CustomerId);
        Assert.AreEqual(1, @event.Lines.Count);
        Assert.AreEqual(OrderStatus.Created, order.Status);
    }

    [TestMethod]
    public void Place_WithNoLines_ShouldReturnFailure()
    {
        var result = Order.Place(Guid.NewGuid(), []);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Place_WithEmptyCustomerId_ShouldReturnFailure()
    {
        var result = Order.Place(Guid.Empty, [Line()]);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void MarkLineReserved_SingleLine_ShouldConfirmOrder()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;

        var result = order.MarkLineReserved(line.Sku);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
        Assert.IsTrue(order.UncommittedEvents.OfType<OrderLineReserved>().Any());
        Assert.IsTrue(order.UncommittedEvents.OfType<OrderConfirmed>().Any());
    }

    [TestMethod]
    public void MarkLineReserved_MultipleLines_ShouldConfirmOnlyAfterAllReserved()
    {
        var lineA = Line("AA");
        var lineB = Line("BB");
        var order = Order.Place(Guid.NewGuid(), [lineA, lineB]).Value!;

        order.MarkLineReserved(lineA.Sku);
        Assert.AreEqual(OrderStatus.Created, order.Status);

        order.MarkLineReserved(lineB.Sku);
        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public void MarkLineReserved_CalledTwiceForSameSku_ShouldNotConfirmTwice()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;

        order.MarkLineReserved(line.Sku);
        var secondResult = order.MarkLineReserved(line.Sku);

        Assert.IsTrue(secondResult.IsFailure);
        Assert.AreEqual(1, order.UncommittedEvents.OfType<OrderConfirmed>().Count());
    }

    [TestMethod]
    public void MarkLineReserved_WithUnknownSku_ShouldReturnFailure()
    {
        var order = Order.Place(Guid.NewGuid(), [Line()]).Value!;
        var unknownSku = SKU.Create(SkuGenerator.Generate("ZZ")).Value!;

        var result = order.MarkLineReserved(unknownSku);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Cancel_ShouldRaiseOrderCancelledEvent()
    {
        var order = Order.Place(Guid.NewGuid(), [Line()]).Value!;

        var result = order.Cancel("Insufficient stock");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
        var @event = order.UncommittedEvents.OfType<OrderCancelled>().FirstOrDefault();
        Assert.IsNotNull(@event);
        Assert.AreEqual("Insufficient stock", @event.Reason);
    }

    [TestMethod]
    public void Cancel_WhenAlreadyConfirmed_ShouldReturnFailure()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(line.Sku);

        var result = order.Cancel("Too late");

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Rehydrate_FromHistory_ShouldRestoreState()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(line.Sku);

        var rehydrated = AggregateRoot.Rehydrate<Order>(order.UncommittedEvents.Reverse());

        Assert.IsTrue(rehydrated.IsSuccess);
        Assert.AreEqual(order.Id, rehydrated.Value!.Id);
        Assert.AreEqual(OrderStatus.Confirmed, rehydrated.Value!.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile (types don't exist yet)**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~OrderTests"`
Expected: Build error — `OrderLine`, `OrderStatus`, `OrderCreated`/`OrderLineReserved`/`OrderConfirmed`/`OrderCancelled` under `Ratatosk.Domain.Ordering.Events`, and `Order.Place`/`MarkLineReserved` don't exist yet.

- [ ] **Step 3: Delete the placeholder files**

```bash
rm src/Domain/Ordering/OrderCreated.cs src/Domain/Ordering/OrderRenamed.cs src/Domain/Ordering/OrderBuilder.cs
```

- [ ] **Step 4: Create `OrderStatus.cs`**

```csharp
namespace Ratatosk.Domain.Ordering;

public enum OrderStatus
{
    Created,
    Confirmed,
    Cancelled,
}
```

- [ ] **Step 5: Create `OrderLine.cs`**

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Ordering;

public sealed class OrderLine : ValueObject
{
    public SKU Sku { get; }
    public int Quantity { get; }
    public Price UnitPrice { get; }

    private OrderLine(SKU sku, int quantity, Price unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public static Result<OrderLine> Create(SKU sku, int quantity, Price unitPrice)
    {
        Guard.AgainstNull(sku, nameof(sku));
        Guard.AgainstNull(unitPrice, nameof(unitPrice));

        if (quantity <= 0)
            return Result<OrderLine>.Failure("Quantity must be greater than zero");

        return Result<OrderLine>.Success(new OrderLine(sku, quantity, unitPrice));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Sku;
        yield return Quantity;
        yield return UnitPrice;
    }
}
```

- [ ] **Step 6: Create the four event files under `src/Domain/Ordering/Events/`**

`OrderCreated.cs`:

```csharp
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderCreated(Guid orderId, Guid customerId, IReadOnlyList<OrderLine> lines)
    : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid CustomerId { get; } = customerId;
    public IReadOnlyList<OrderLine> Lines { get; } = lines;
}
```

`OrderLineReserved.cs`:

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderLineReserved(Guid orderId, SKU sku) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public SKU Sku { get; } = sku;
}
```

`OrderConfirmed.cs`:

```csharp
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderConfirmed(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}
```

`OrderCancelled.cs`:

```csharp
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderCancelled(Guid orderId, string reason, IReadOnlyList<OrderLine> lines)
    : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string Reason { get; } = reason;
    public IReadOnlyList<OrderLine> Lines { get; } = lines;
}
```

- [ ] **Step 7: Rewrite `Order.cs`**

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Domain.Ordering;

public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    private readonly HashSet<SKU> _reservedSkus = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderCreated e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                _lines.AddRange(e.Lines);
                Status = OrderStatus.Created;
                break;

            case OrderLineReserved e:
                _reservedSkus.Add(e.Sku);
                break;

            case OrderConfirmed:
                Status = OrderStatus.Confirmed;
                break;

            case OrderCancelled:
                Status = OrderStatus.Cancelled;
                break;
        }
    }

    public static Result<Order> Place(Guid customerId, IEnumerable<OrderLine> lines)
    {
        try
        {
            Guard.AgainstEmpty(customerId, nameof(customerId));

            var lineList = lines?.ToList() ?? [];
            if (lineList.Count == 0)
                return Result<Order>.Failure("Order must contain at least one line");

            var order = new Order();
            order.RaiseEvent(new OrderCreated(order.Id, customerId, lineList));

            return Result<Order>.Success(order);
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result<Order>.Failure(error.Message);
        }
    }

    public Result MarkLineReserved(SKU sku)
    {
        if (Status != OrderStatus.Created)
            return Result.Failure($"Cannot reserve a line for an order in status {Status}");

        if (!_lines.Any(l => l.Sku.Equals(sku)))
            return Result.Failure($"SKU {sku} is not part of this order");

        if (_reservedSkus.Contains(sku))
            return Result.Failure($"SKU {sku} was already marked as reserved");

        RaiseEvent(new OrderLineReserved(Id, sku));

        if (_lines.Select(l => l.Sku).All(_reservedSkus.Contains))
            RaiseEvent(new OrderConfirmed(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        try
        {
            Guard.AgainstNullOrEmpty(reason, nameof(reason));

            if (Status != OrderStatus.Created)
                return Result.Failure($"Cannot cancel an order in status {Status}");

            RaiseEvent(new OrderCancelled(Id, reason, _lines));

            return Result.Success();
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result.Failure(error.Message);
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~OrderTests"`
Expected: All `OrderTests` PASS.

- [ ] **Step 9: Build the whole solution to catch any other break from the deleted placeholder files**

Run: `dotnet build`
Expected: Build succeeds (no other file referenced `OrderBuilder`/old `OrderCreated`/`OrderRenamed`).

- [ ] **Step 10: Commit**

```bash
git add src/Domain/Ordering tests/UnitTests/Domain/Ordering/OrderTests.cs
git commit -m "feat(ordering): implement Order aggregate with line-by-line reservation tracking"
```

---

### Task 2: Correlate Inventoring reservation events with an order id

**Files:**
- Modify: `src/Domain/Inventoring/Events/StockReserved.cs`
- Create: `src/Domain/Inventoring/Events/StockReservationFailed.cs`
- Modify: `src/Domain/Inventoring/Inventory.cs` (`ReserveStock` method)
- Modify: `tests/UnitTests/Domain/Inventoring/InventoryTests.cs` (append new tests; existing tests must keep passing unmodified)

**Interfaces:**
- Consumes: nothing new from Task 1.
- Produces: `StockReserved(Guid InventoryId, SKU SKU, int Quantity, Guid? OrderId = null)`; `StockReservationFailed(Guid InventoryId, SKU SKU, Guid OrderId, int Quantity, string Reason)`; `Inventory.ReserveStock(SKU sku, int quantity, Guid? orderId = null)` (unchanged behavior when `orderId` is `null`: throws `InvalidOperationException` on insufficient stock; when `orderId` is provided, raises `StockReservationFailed` instead of throwing).

- [ ] **Step 1: Write the failing tests**

Append to `tests/UnitTests/Domain/Inventoring/InventoryTests.cs` (inside the existing `InventoryTests` class, add `using Ratatosk.Domain.Inventoring.Events;` is already present):

```csharp
    [TestMethod]
    public void ReserveStock_WithOrderId_ShouldRaiseStockReservedEventWithOrderId()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var orderId = Guid.NewGuid();

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 5, orderId);

        var @event = inventory.UncommittedEvents.OfType<StockReserved>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(orderId, @event.OrderId);
    }

    [TestMethod]
    public void ReserveStock_WithOrderIdAndInsufficientStock_ShouldRaiseStockReservationFailedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var orderId = Guid.NewGuid();

        inventory.AddStock(sku, Quantity.Pieces(3));
        inventory.ReserveStock(sku, 5, orderId);

        var @event = inventory
            .UncommittedEvents.OfType<StockReservationFailed>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(orderId, @event.OrderId);
        Assert.AreEqual(5, @event.Quantity);
        Assert.IsFalse(inventory.UncommittedEvents.OfType<StockReserved>().Any());
    }
```

The existing `ReserveStock_WithInsufficientStock_ShouldThrowException` test (no `orderId` argument) must be left exactly as-is — it pins the Review Focus item that manual reservations keep throwing.

- [ ] **Step 2: Run tests to verify the new ones fail to compile**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~InventoryTests"`
Expected: Build error — `StockReservationFailed` doesn't exist yet, `ReserveStock` doesn't accept a third argument, `StockReserved.OrderId` doesn't exist.

- [ ] **Step 3: Modify `StockReserved.cs`**

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockReserved(Guid inventoryId, SKU sku, int quantity, Guid? orderId = null)
    : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public int Quantity { get; } = quantity;
    public Guid? OrderId { get; } = orderId;
}
```

- [ ] **Step 4: Create `StockReservationFailed.cs`**

```csharp
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockReservationFailed(
    Guid inventoryId,
    SKU sku,
    Guid orderId,
    int quantity,
    string reason
) : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public Guid OrderId { get; } = orderId;
    public int Quantity { get; } = quantity;
    public string Reason { get; } = reason;
}
```

- [ ] **Step 5: Modify `Inventory.ReserveStock`**

In `src/Domain/Inventoring/Inventory.cs`, replace the `ReserveStock` method with:

```csharp
    public void ReserveStock(SKU sku, int quantity, Guid? orderId = null)
    {
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        if (!_stockBySku.TryGetValue(sku, out var stockEntry))
        {
            throw new InvalidOperationException($"SKU {sku} not found in inventory");
        }

        if (stockEntry.Available.Amount - stockEntry.Reserved < quantity)
        {
            if (orderId is Guid correlatedOrderId)
            {
                RaiseEvent(
                    new StockReservationFailed(
                        Id,
                        sku,
                        correlatedOrderId,
                        quantity,
                        "Not enough stock available"
                    )
                );
                return;
            }

            throw new InvalidOperationException($"Not enough stock available for SKU {sku}");
        }

        RaiseEvent(new StockReserved(Id, sku, quantity, orderId));
    }
```

Add `using Ratatosk.Domain.Inventoring.Events;` at the top of `Inventory.cs` if not already present (it already imports this namespace for `StockAdded` etc.).

- [ ] **Step 6: Run tests to verify they pass, including the pre-existing ones**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~InventoryTests"`
Expected: All tests in `InventoryTests` PASS, including `ReserveStock_WithInsufficientStock_ShouldThrowException` unchanged.

- [ ] **Step 7: Commit**

```bash
git add src/Domain/Inventoring tests/UnitTests/Domain/Inventoring/InventoryTests.cs
git commit -m "feat(inventoring): correlate stock reservation events with an order id"
```

---

### Task 3: Inventoring-side handlers (reserve on order placed, compensate on order cancelled)

**Files:**
- Create: `src/Application/Inventoring/OrderReservationHandler.cs`
- Create: `src/Application/Inventoring/ReservationCompensationHandler.cs`
- Test: `tests/UnitTests/Application/Inventoring/OrderReservationHandlerTests.cs`
- Test: `tests/UnitTests/Application/Inventoring/ReservationCompensationHandlerTests.cs`

**Interfaces:**
- Consumes: `Order.Events.OrderCreated`, `Order.Events.OrderCancelled` (Task 1); `Inventory.ReserveStock(sku, quantity, orderId)`, `Inventory.ReleaseStock(sku, quantity)`, `StockReservationFailed` (Task 2); `IInventoryReadModelRepository.GetBySkuAsync(string, CancellationToken)` (existing); `IAggregateRepository<Inventory>.LoadAsync`/`SaveAsync` (existing); `IEventBus.PublishAsync` (existing).
- Produces: `OrderReservationHandler : IDomainEventHandler<OrderCreated>`, `ReservationCompensationHandler : IDomainEventHandler<OrderCancelled>` — both auto-registered by the existing `AddProjections()` assembly scan, no manual DI needed.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/Application/Inventoring/OrderReservationHandlerTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class OrderReservationHandlerTests
{
    private Mock<IInventoryReadModelRepository> _readModelRepoMock = null!;
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private OrderReservationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new OrderReservationHandler(
            _readModelRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    private static OrderLine Line(SKU sku, int quantity) =>
        OrderLine.Create(sku, quantity, Price.Create(10m).Value!).Value!;

    [TestMethod]
    public async Task WhenStockAvailable_ShouldReserveAndPublishStockReserved()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e => e is StockReserved sr && sr.OrderId == orderId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenSkuHasNoInventoryRecord_ShouldPublishStockReservationFailed()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var orderId = Guid.NewGuid();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e => e is StockReservationFailed f && f.OrderId == orderId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
```

Create `tests/UnitTests/Application/Inventoring/ReservationCompensationHandlerTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class ReservationCompensationHandlerTests
{
    private Mock<IInventoryReadModelRepository> _readModelRepoMock = null!;
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private ReservationCompensationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new ReservationCompensationHandler(
            _readModelRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    private static OrderLine Line(SKU sku, int quantity) =>
        OrderLine.Create(sku, quantity, Price.Create(10m).Value!).Value!;

    [TestMethod]
    public async Task WhenLineWasReserved_ShouldReleaseAndPublishStockReleased()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3);
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 7, 3, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(Guid.NewGuid(), "Insufficient stock", [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e => e is StockReleased),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenLineWasNeverReserved_ShouldNotThrow()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(Guid.NewGuid(), "Insufficient stock", [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~OrderReservationHandlerTests|FullyQualifiedName~ReservationCompensationHandlerTests"`
Expected: Build error — `OrderReservationHandler`/`ReservationCompensationHandler` don't exist yet.

- [ ] **Step 3: Create `OrderReservationHandler.cs`**

```csharp
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Inventoring;

public class OrderReservationHandler(
    IInventoryReadModelRepository readModelRepository,
    IAggregateRepository<Inventory> repository,
    IEventBus eventBus
) : IDomainEventHandler<OrderCreated>
{
    public async Task WhenAsync(
        OrderCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var line in domainEvent.Lines)
        {
            var readModel = await readModelRepository.GetBySkuAsync(
                line.Sku.Value,
                cancellationToken
            );
            if (readModel is null)
            {
                await eventBus.PublishAsync(
                    new StockReservationFailed(
                        Guid.Empty,
                        line.Sku,
                        domainEvent.OrderId,
                        line.Quantity,
                        $"SKU {line.Sku} has no inventory record"
                    ),
                    cancellationToken
                );
                continue;
            }

            var inventoryResult = await repository.LoadAsync(
                readModel.ProductId,
                cancellationToken
            );
            if (inventoryResult.IsFailure)
            {
                await eventBus.PublishAsync(
                    new StockReservationFailed(
                        readModel.ProductId,
                        line.Sku,
                        domainEvent.OrderId,
                        line.Quantity,
                        inventoryResult.Error ?? "Failed to load inventory"
                    ),
                    cancellationToken
                );
                continue;
            }

            var inventory = inventoryResult.Value!;
            inventory.ReserveStock(line.Sku, line.Quantity, domainEvent.OrderId);

            await repository.SaveAsync(inventory, cancellationToken);

            foreach (var raised in inventory.UncommittedEvents)
                await eventBus.PublishAsync(raised, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Create `ReservationCompensationHandler.cs`**

```csharp
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Inventoring;

public class ReservationCompensationHandler(
    IInventoryReadModelRepository readModelRepository,
    IAggregateRepository<Inventory> repository,
    IEventBus eventBus
) : IDomainEventHandler<OrderCancelled>
{
    public async Task WhenAsync(
        OrderCancelled domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var line in domainEvent.Lines)
        {
            var readModel = await readModelRepository.GetBySkuAsync(
                line.Sku.Value,
                cancellationToken
            );
            if (readModel is null)
                continue;

            var inventoryResult = await repository.LoadAsync(
                readModel.ProductId,
                cancellationToken
            );
            if (inventoryResult.IsFailure)
                continue;

            var inventory = inventoryResult.Value!;

            try
            {
                inventory.ReleaseStock(line.Sku, line.Quantity);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            await repository.SaveAsync(inventory, cancellationToken);

            foreach (var raised in inventory.UncommittedEvents)
                await eventBus.PublishAsync(raised, cancellationToken);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~OrderReservationHandlerTests|FullyQualifiedName~ReservationCompensationHandlerTests"`
Expected: All PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Application/Inventoring tests/UnitTests/Application/Inventoring
git commit -m "feat(inventoring): react to order placement and cancellation events"
```

---

### Task 4: Ordering-side handlers and read-model projection

**Files:**
- Create: `src/Application/Ordering/Models/OrderReadModel.cs` (contains `OrderReadModel` and `OrderLineReadModel`)
- Create: `src/Application/Ordering/IOrderReadModelRepository.cs`
- Create: `src/Application/Ordering/OrderProjection.cs`
- Create: `src/Application/Ordering/OrderConfirmationHandler.cs`
- Create: `src/Application/Ordering/OrderCancellationHandler.cs`
- Test: `tests/UnitTests/Application/Ordering/OrderProjectionTests.cs`
- Test: `tests/UnitTests/Application/Ordering/OrderConfirmationHandlerTests.cs`
- Test: `tests/UnitTests/Application/Ordering/OrderCancellationHandlerTests.cs`

**Interfaces:**
- Consumes: `Domain.Ordering.Events.*` (Task 1), `Domain.Inventoring.Events.StockReserved`/`StockReservationFailed` (Task 2), `IAggregateRepository<Order>` (existing generic), `IEventBus` (existing).
- Produces: `IOrderReadModelRepository` (`GetByIdAsync`, `SaveAsync`); `OrderReadModel`/`OrderLineReadModel` (used by Task 5 and Task 6); `OrderProjection`, `OrderConfirmationHandler`, `OrderCancellationHandler` — auto-registered by `AddProjections()`.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/Application/Ordering/OrderProjectionTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class OrderProjectionTests
{
    private Mock<IOrderReadModelRepository> _repoMock = null!;
    private OrderProjection _projection = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IOrderReadModelRepository>();
        _projection = new OrderProjection(_repoMock.Object);
    }

    [TestMethod]
    public async Task WhenOrderCreated_ShouldSaveReadModelWithCreatedStatus()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var evt = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), [line]);

        OrderReadModel? saved = null;
        _repoMock
            .Setup(r => r.SaveAsync(It.IsAny<OrderReadModel>(), It.IsAny<CancellationToken>()))
            .Callback<OrderReadModel, CancellationToken>((rm, _) => saved = rm);

        await _projection.WhenAsync(evt, CancellationToken.None);

        Assert.IsNotNull(saved);
        Assert.AreEqual(evt.OrderId, saved!.Id);
        Assert.AreEqual(nameof(OrderStatus.Created), saved.Status);
        Assert.AreEqual(1, saved.Lines.Count);
    }

    [TestMethod]
    public async Task WhenOrderConfirmed_ShouldUpdateStatus()
    {
        var orderId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderReadModel { Id = orderId, Status = nameof(OrderStatus.Created) });

        await _projection.WhenAsync(new OrderConfirmed(orderId), CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<OrderReadModel>(rm => rm.Status == nameof(OrderStatus.Confirmed)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
```

Create `tests/UnitTests/Application/Ordering/OrderConfirmationHandlerTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class OrderConfirmationHandlerTests
{
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private OrderConfirmationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new OrderConfirmationHandler(_repositoryMock.Object, _eventBusMock.Object);
    }

    [TestMethod]
    public async Task WhenStockReservedHasOrderId_ShouldMarkLineReserved()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.ClearUncommittedEvents();

        _repositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        var evt = new StockReserved(Guid.NewGuid(), sku, 2, order.Id);

        await _handler.WhenAsync(evt, CancellationToken.None);

        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public async Task WhenStockReservedHasNoOrderId_ShouldDoNothing()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var evt = new StockReserved(Guid.NewGuid(), sku, 2, null);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
```

Create `tests/UnitTests/Application/Ordering/OrderCancellationHandlerTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class OrderCancellationHandlerTests
{
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private OrderCancellationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new OrderCancellationHandler(_repositoryMock.Object, _eventBusMock.Object);
    }

    [TestMethod]
    public async Task WhenStockReservationFailed_ShouldCancelOrder()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.ClearUncommittedEvents();

        _repositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        var evt = new StockReservationFailed(Guid.NewGuid(), sku, order.Id, 2, "Not enough stock");

        await _handler.WhenAsync(evt, CancellationToken.None);

        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~Ratatosk.UnitTests.Application.Ordering"`
Expected: Build errors — none of the new types exist yet.

- [ ] **Step 3: Create `Models/OrderReadModel.cs`**

```csharp
namespace Ratatosk.Application.Ordering.Models;

public class OrderLineReadModel
{
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = default!;

    public OrderLineReadModel() { }

    public OrderLineReadModel(string sku, int quantity, decimal unitPrice, string currency)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Currency = currency;
    }
}

public class OrderReadModel
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = default!;
    public List<OrderLineReadModel> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
```

- [ ] **Step 4: Create `IOrderReadModelRepository.cs`**

```csharp
using Ratatosk.Application.Ordering.Models;

namespace Ratatosk.Application.Ordering;

public interface IOrderReadModelRepository
{
    Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(OrderReadModel order, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Create `OrderProjection.cs`**

```csharp
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Ordering;

public class OrderProjection(IOrderReadModelRepository repo)
    : IDomainEventHandler<OrderCreated>,
        IDomainEventHandler<OrderConfirmed>,
        IDomainEventHandler<OrderCancelled>
{
    public async Task WhenAsync(
        OrderCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = new OrderReadModel
        {
            Id = domainEvent.OrderId,
            CustomerId = domainEvent.CustomerId,
            Status = nameof(OrderStatus.Created),
            Lines =
            [
                .. domainEvent.Lines.Select(l => new OrderLineReadModel(
                    l.Sku.Value,
                    l.Quantity,
                    l.UnitPrice.Amount,
                    l.UnitPrice.Currency
                )),
            ],
            CreatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(
        OrderConfirmed domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await repo.GetByIdAsync(domainEvent.OrderId, cancellationToken);
        if (existing is null)
            return;

        existing.Status = nameof(OrderStatus.Confirmed);
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(
        OrderCancelled domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await repo.GetByIdAsync(domainEvent.OrderId, cancellationToken);
        if (existing is null)
            return;

        existing.Status = nameof(OrderStatus.Cancelled);
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }
}
```

- [ ] **Step 6: Create `OrderConfirmationHandler.cs`**

```csharp
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering;

public class OrderConfirmationHandler(IAggregateRepository<Order> repository, IEventBus eventBus)
    : IDomainEventHandler<StockReserved>
{
    public async Task WhenAsync(
        StockReserved domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        if (domainEvent.OrderId is not Guid orderId)
            return;

        var orderResult = await repository.LoadAsync(orderId, cancellationToken);
        if (orderResult.IsFailure)
            return;

        var order = orderResult.Value!;
        order.MarkLineReserved(domainEvent.SKU);

        await repository.SaveAsync(order, cancellationToken);

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
```

- [ ] **Step 7: Create `OrderCancellationHandler.cs`**

```csharp
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering;

public class OrderCancellationHandler(IAggregateRepository<Order> repository, IEventBus eventBus)
    : IDomainEventHandler<StockReservationFailed>
{
    public async Task WhenAsync(
        StockReservationFailed domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var orderResult = await repository.LoadAsync(domainEvent.OrderId, cancellationToken);
        if (orderResult.IsFailure)
            return;

        var order = orderResult.Value!;
        order.Cancel(domainEvent.Reason);

        await repository.SaveAsync(order, cancellationToken);

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~Ratatosk.UnitTests.Application.Ordering"`
Expected: All PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Application/Ordering tests/UnitTests/Application/Ordering
git commit -m "feat(ordering): add read-model projection and reservation-outcome handlers"
```

---

### Task 5: `PlaceOrderCommand`, `GetOrderByIdQuery`, and `IOrderService`

**Files:**
- Create: `src/Application/Ordering/Commands/PlaceOrderCommand.cs` (contains `OrderLineRequest`, `PlaceOrderCommand`, `PlaceOrderCommandHandler`)
- Create: `src/Application/Ordering/Queries/GetOrderByIdQuery.cs`
- Create: `src/Application/Ordering/IOrderService.cs` (contains `IOrderService` and `OrderService`)
- Test: `tests/UnitTests/Application/Ordering/PlaceOrderCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IInventoryReadModelRepository.GetBySkuAsync` (existing), `IProductReadModelRepository.GetByIdAsync` (existing), `IAggregateRepository<Order>` (existing generic), `IEventBus` (existing), `IOrderReadModelRepository` (Task 4), `IDispatcher`/`IUnitOfWork` (existing, used the same way as `InventoryService`).
- Produces: `PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineRequest> Lines) : IRequest<Result<Guid>>`; `GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderReadModel>>`; `IOrderService.PlaceOrderAsync`/`GetOrderByIdAsync` — consumed by Task 7's API endpoints.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/Application/Ordering/PlaceOrderCommandHandlerTests.cs`:

```csharp
using Moq;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class PlaceOrderCommandHandlerTests
{
    private Mock<IInventoryReadModelRepository> _inventoryRepoMock = null!;
    private Mock<IProductReadModelRepository> _productRepoMock = null!;
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private PlaceOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _inventoryRepoMock = new Mock<IInventoryReadModelRepository>();
        _productRepoMock = new Mock<IProductReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new PlaceOrderCommandHandler(
            _inventoryRepoMock.Object,
            _productRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    [TestMethod]
    public async Task WhenSkuAndProductExist_ShouldPlaceOrderAndReturnOrderId()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow)
            );
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductReadModel(
                    productId,
                    "Widget",
                    sku.Value,
                    "A widget",
                    9.99m,
                    DateTime.UtcNow
                )
            );

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 2)]);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreNotEqual(Guid.Empty, result.Value);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenSkuHasNoInventoryRecord_ShouldReturnFailureWithoutSaving()
    {
        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest("XX-000000", 2)]);

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync("XX-000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task WhenLineQuantityIsZeroOrLess_ShouldReturnFailureWithoutSaving()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow)
            );
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductReadModel(
                    productId,
                    "Widget",
                    sku.Value,
                    "A widget",
                    9.99m,
                    DateTime.UtcNow
                )
            );

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 0)]);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~PlaceOrderCommandHandlerTests"`
Expected: Build error — `PlaceOrderCommand`/`PlaceOrderCommandHandler`/`OrderLineRequest` don't exist yet.

- [ ] **Step 3: Create `Commands/PlaceOrderCommand.cs`**

```csharp
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Inventoring;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering.Commands;

public sealed record OrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineRequest> Lines)
    : IRequest<Result<Guid>>;

public class PlaceOrderCommandHandler(
    IInventoryReadModelRepository inventoryReadModelRepository,
    IProductReadModelRepository productReadModelRepository,
    IAggregateRepository<Order> repository,
    IEventBus eventBus
) : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(
        PlaceOrderCommand request,
        CancellationToken cancellationToken = default
    )
    {
        var lines = new List<OrderLine>();

        foreach (var lineRequest in request.Lines)
        {
            var skuResult = SKU.Create(lineRequest.Sku);
            if (skuResult.IsFailure)
                return Result<Guid>.Failure(skuResult.Error!);

            var stockReadModel = await inventoryReadModelRepository.GetBySkuAsync(
                lineRequest.Sku,
                cancellationToken
            );
            if (stockReadModel is null)
                return Result<Guid>.Failure($"SKU {lineRequest.Sku} not found");

            var productReadModel = await productReadModelRepository.GetByIdAsync(
                stockReadModel.ProductId,
                cancellationToken
            );
            if (productReadModel is null)
                return Result<Guid>.Failure($"Product for SKU {lineRequest.Sku} not found");

            var priceResult = Price.Create(productReadModel.Price);
            if (priceResult.IsFailure)
                return Result<Guid>.Failure(priceResult.Error!);

            var lineResult = OrderLine.Create(
                skuResult.Value!,
                lineRequest.Quantity,
                priceResult.Value!
            );
            if (lineResult.IsFailure)
                return Result<Guid>.Failure(lineResult.Error!);

            lines.Add(lineResult.Value!);
        }

        var orderResult = Order.Place(request.CustomerId, lines);
        if (orderResult.IsFailure)
            return Result<Guid>.Failure(orderResult.Error!);

        var order = orderResult.Value!;
        await repository.SaveAsync(order, cancellationToken);

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);

        return Result<Guid>.Success(order.Id);
    }
}
```

- [ ] **Step 4: Create `Queries/GetOrderByIdQuery.cs`**

```csharp
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering.Queries;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderReadModel>>;

public class GetOrderByIdQueryHandler(IOrderReadModelRepository repository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderReadModel>>
{
    public async Task<Result<OrderReadModel>> HandleAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var order = await repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderReadModel>.Failure($"No order found with id {query.OrderId}");

        return Result<OrderReadModel>.Success(order);
    }
}
```

- [ ] **Step 5: Create `IOrderService.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Ordering.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering;

public interface IOrderService
{
    Task<Result<Guid>> PlaceOrderAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result<OrderReadModel>> GetOrderByIdAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    );
}

public class OrderService(IDispatcher dispatcher, IUnitOfWork uow, ILogger<OrderService> logger)
    : IOrderService
{
    public async Task<Result<Guid>> PlaceOrderAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to place order: {Error}", result.Error);
        else
            uow.Commit();

        return result;
    }

    public async Task<Result<OrderReadModel>> GetOrderByIdAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to fetch order: {Error}", result.Error);

        return result;
    }
}
```

- [ ] **Step 6: Register `IOrderService` in `Application/DependencyInjection.cs`**

In `src/Application/DependencyInjection.cs`, add `using Ratatosk.Application.Ordering;` to the usings, and add this line next to the other `AddScoped<I...Service, ...>()` calls inside `AddApplication`:

```csharp
        services.AddScoped<IOrderService, OrderService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/UnitTests --filter "FullyQualifiedName~PlaceOrderCommandHandlerTests"`
Expected: All PASS.

- [ ] **Step 8: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 9: Commit**

```bash
git add src/Application/Ordering src/Application/DependencyInjection.cs tests/UnitTests/Application/Ordering/PlaceOrderCommandHandlerTests.cs
git commit -m "feat(ordering): add PlaceOrder/GetOrderById use cases and IOrderService"
```

---

### Task 6: Postgres read-model persistence, JSON serialization, and DI wiring

**Files:**
- Create: `src/Infrastructure/Persistence/ReadModels/OrderReadModel.cs` (class `OrderReadModelRepository`)
- Modify: `src/Infrastructure/Serialization/Converters/ValueObjectConverters.cs` (add `OrderLineConverter`)
- Modify: `src/Infrastructure/Serialization/JsonPolymorphicSerializer.cs` (register `OrderLineConverter`)
- Modify: `src/Infrastructure/DependencyInjection.cs` (register `IOrderReadModelRepository`)
- Modify: `postgres/init/01-init.sql` (add `order_read_models`, `order_line_read_models` tables)
- Test: `tests/IntegrationTests/PostgresOrderReadModelRepositoryTests.cs`

**Interfaces:**
- Consumes: `IOrderReadModelRepository` (Task 4), `OrderLine`/`OrderReadModel`/`OrderLineReadModel` (Tasks 1 and 4), `PostgresRepository` base (existing), `SKUConverter`/`PriceConverter` (existing, reused by `OrderLineConverter`).
- Produces: `OrderReadModelRepository : IOrderReadModelRepository` — consumed by DI and by the integration test.

- [ ] **Step 1: Add the SQL tables**

Append to `postgres/init/01-init.sql`:

```sql
CREATE TABLE IF NOT EXISTS order_read_models(
    id uuid PRIMARY KEY,
    customer_id uuid NOT NULL,
    status text NOT NULL,
    created_utc timestamptz NOT NULL,
    last_updated_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS order_line_read_models(
    order_id uuid NOT NULL REFERENCES order_read_models(id),
    sku text NOT NULL,
    quantity integer NOT NULL,
    unit_price decimal NOT NULL,
    currency text NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_order_line_read_models_order_id ON order_line_read_models(order_id);
```

- [ ] **Step 2: Write the failing integration test**

Create `tests/IntegrationTests/PostgresOrderReadModelRepositoryTests.cs`, matching the exact pattern of the existing `PostgresInventoryReadModelRepositoryTests.cs` (no shared base class — each test class opens its own `IUnitOfWork` against the Docker Postgres on port 5433 in `[TestInitialize]`, creates/truncates its own tables, and rolls back in `[TestCleanup]`):

```csharp
using Dapper;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresOrderReadModelRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;
    private IOrderReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        await _uow.Connection.ExecuteAsync(
            """
                DROP TABLE IF EXISTS order_line_read_models;
                DROP TABLE IF EXISTS order_read_models;
                CREATE TABLE IF NOT EXISTS order_read_models(
                    id uuid PRIMARY KEY,
                    customer_id uuid NOT NULL,
                    status text NOT NULL,
                    created_utc timestamptz NOT NULL,
                    last_updated_utc timestamptz NOT NULL
                );
                CREATE TABLE IF NOT EXISTS order_line_read_models(
                    order_id uuid NOT NULL REFERENCES order_read_models(id),
                    sku text NOT NULL,
                    quantity integer NOT NULL,
                    unit_price decimal NOT NULL,
                    currency text NOT NULL
                );
            """,
            transaction: _uow.Transaction
        );

        _repo = new OrderReadModelRepository(_uow);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_uow == null)
        {
            return;
        }

        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_ShouldInsertOrderWithLines()
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = "Created",
            Lines = [new OrderLineReadModel("TS-ABCD1234", 2, 9.99m, "SEK")],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
        };

        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);

        Assert.IsNotNull(fetched);
        Assert.AreEqual(order.CustomerId, fetched!.CustomerId);
        Assert.AreEqual(1, fetched.Lines.Count);
        Assert.AreEqual("TS-ABCD1234", fetched.Lines[0].Sku);
    }

    [TestMethod]
    public async Task SaveAsync_WhenRecordAlreadyExists_ShouldReplaceLines()
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = "Created",
            Lines = [new OrderLineReadModel("TS-ABCD1234", 2, 9.99m, "SEK")],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
        };
        await _repo.SaveAsync(order, TestContext.CancellationToken);

        order.Status = "Confirmed";
        order.Lines = [new OrderLineReadModel("TS-ABCD1234", 5, 9.99m, "SEK")];
        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);

        Assert.IsNotNull(fetched);
        Assert.AreEqual("Confirmed", fetched!.Status);
        Assert.AreEqual(1, fetched.Lines.Count);
        Assert.AreEqual(5, fetched.Lines[0].Quantity);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    public TestContext TestContext { get; set; }
}
```

- [ ] **Step 3: Run the integration test to verify it fails to compile**

Run: `./scripts/run-integration-tests.sh` (or `dotnet test tests/IntegrationTests --filter "FullyQualifiedName~PostgresOrderReadModelRepositoryTests"` against the Docker Postgres it spins up)
Expected: Build error — `OrderReadModelRepository` doesn't exist yet.

- [ ] **Step 4: Create `OrderReadModelRepository.cs`**

```csharp
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class OrderReadModelRepository(IUnitOfWork uow)
    : PostgresRepository(uow),
      IOrderReadModelRepository
{
    public async Task<OrderReadModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var order = await QueryFirstOrDefaultAsync<OrderReadModel>(
            """
                SELECT id, customer_id, status, created_utc, last_updated_utc
                FROM order_read_models
                WHERE id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

        if (order is null)
            return null;

        var lines = await QueryAsync<OrderLineReadModel>(
            """
                SELECT sku, quantity, unit_price, currency
                FROM order_line_read_models
                WHERE order_id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

        order.Lines = [.. lines];
        return order;
    }

    public async Task SaveAsync(
        OrderReadModel order,
        CancellationToken cancellationToken = default
    )
    {
        await ExecAsync(
            """
                INSERT INTO order_read_models (id, customer_id, status, created_utc, last_updated_utc)
                VALUES (@Id, @CustomerId, @Status, @CreatedUtc, @LastUpdatedUtc)
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    last_updated_utc = EXCLUDED.last_updated_utc
            """,
            order,
            cancellationToken
        );

        await ExecAsync(
            """
                DELETE FROM order_line_read_models
                WHERE order_id = @Id
            """,
            new { order.Id },
            cancellationToken
        );

        foreach (var line in order.Lines)
        {
            await ExecAsync(
                """
                    INSERT INTO order_line_read_models (order_id, sku, quantity, unit_price, currency)
                    VALUES (@OrderId, @Sku, @Quantity, @UnitPrice, @Currency)
                """,
                new
                {
                    OrderId = order.Id,
                    line.Sku,
                    line.Quantity,
                    line.UnitPrice,
                    line.Currency,
                },
                cancellationToken
            );
        }
    }
}
```

- [ ] **Step 5: Register `IOrderReadModelRepository` in `Infrastructure/DependencyInjection.cs`**

Add `using Ratatosk.Application.Ordering;` to the usings, and add this line next to `services.AddScoped<IInventoryReadModelRepository, InventoryReadModelRepository>();`:

```csharp
        services.AddScoped<IOrderReadModelRepository, OrderReadModelRepository>();
```

- [ ] **Step 6: Add `OrderLineConverter` to `ValueObjectConverters.cs`**

Add `using Ratatosk.Domain.Ordering;` to the top of `src/Infrastructure/Serialization/Converters/ValueObjectConverters.cs`, and append:

```csharp
public class OrderLineConverter : JsonConverter<OrderLine>
{
    public override OrderLine Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        SKU? sku = null;
        int? quantity = null;
        Price? unitPrice = null;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for OrderLine");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in OrderLine object");
            }

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "Sku":
                    sku = JsonSerializer.Deserialize<SKU>(ref reader, options);
                    break;
                case "Quantity":
                    quantity = reader.GetInt32();
                    break;
                case "UnitPrice":
                    unitPrice = JsonSerializer.Deserialize<Price>(ref reader, options);
                    break;
            }
        }

        if (sku is null || quantity is null || unitPrice is null)
            throw new JsonException("Missing required OrderLine properties");

        var result = OrderLine.Create(sku, quantity.Value, unitPrice);

        if (!result.IsSuccess)
            throw new JsonException($"Invalid OrderLine: {result.Error}");

        return result.Value!;
    }

    public override void Write(
        Utf8JsonWriter writer,
        OrderLine value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Sku");
        JsonSerializer.Serialize(writer, value.Sku, options);
        writer.WriteNumber("Quantity", value.Quantity);
        writer.WritePropertyName("UnitPrice");
        JsonSerializer.Serialize(writer, value.UnitPrice, options);
        writer.WriteEndObject();
    }
}
```

- [ ] **Step 7: Register the converter in `JsonPolymorphicSerializer.cs`**

In `src/Infrastructure/Serialization/JsonPolymorphicSerializer.cs`, add `new OrderLineConverter(),` to the `Converters` list alongside `ProductNameConverter`, `SKUConverter`, `DescriptionConverter`, `PriceConverter`.

- [ ] **Step 8: Run the integration test to verify it passes**

Run: `./scripts/run-integration-tests.sh`
Expected: `PostgresOrderReadModelRepositoryTests` PASSES along with all other integration tests.

- [ ] **Step 9: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
git add src/Infrastructure postgres/init/01-init.sql tests/IntegrationTests/PostgresOrderReadModelRepositoryTests.cs
git commit -m "feat(ordering): add Postgres read-model persistence and event serialization"
```

---

### Task 7: API endpoints

**Files:**
- Create: `src/API/Orders/OrderEndpoints.cs`
- Modify: `src/API/Program.cs`

**Interfaces:**
- Consumes: `IOrderService` (Task 5), `PlaceOrderCommand`/`OrderLineRequest` (Task 5), `GetOrderByIdQuery` (Task 5), `OrderReadModel` (Task 4), `Response`/`Response<T>`/`Policies` (existing, in `Ratatosk.API`, visible without a `using` from `Ratatosk.API.Orders` per the same convention `InventoryEndpoints.cs` relies on).
- Produces: `app.MapOrderEndpoints()` — called from `Program.cs`.

- [ ] **Step 1: Create `OrderEndpoints.cs`**

```csharp
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Ordering.Queries;

namespace Ratatosk.API.Orders;

public static class OrderEndpoints
{
    private const string OrdersTag = "Orders";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/orders",
                async (
                    PlaceOrderRequest request,
                    IOrderService orderService,
                    CancellationToken ct
                ) =>
                {
                    var command = new PlaceOrderCommand(
                        request.CustomerId,
                        [.. request.Lines.Select(l => new OrderLineRequest(l.Sku, l.Quantity))]
                    );
                    var result = await orderService.PlaceOrderAsync(command, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure
                        ? Results.BadRequest(response)
                        : Results.Accepted($"/orders/{result.Value}", response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("PlaceOrder")
            .WithSummary("Place a new order")
            .WithDescription(
                "Creates an order and asynchronously reserves stock for each line. "
                    + "Poll GET /orders/{orderId} to observe confirmation or cancellation."
            )
            .Accepts<PlaceOrderRequest>("application/json")
            .Produces<Response>(StatusCodes.Status202Accepted)
            .Produces<Response>(StatusCodes.Status400BadRequest);

        app.MapGet(
                "/orders/{orderId:guid}",
                async (Guid orderId, IOrderService orderService, CancellationToken ct) =>
                {
                    var query = new GetOrderByIdQuery(orderId);
                    var result = await orderService.GetOrderByIdAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .WithTags(OrdersTag)
            .WithName("GetOrderById")
            .WithSummary("Get an order by id")
            .WithDescription("Returns the current read model for the supplied order id.")
            .Produces<OrderReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record PlaceOrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderRequest(Guid CustomerId, IReadOnlyList<PlaceOrderLineRequest> Lines);
```

- [ ] **Step 2: Wire it up in `Program.cs`**

Add `using Ratatosk.API.Orders;` to the usings at the top, and add `app.MapOrderEndpoints();` next to `app.MapInventoryEndpoints();`.

- [ ] **Step 3: Build and run the API**

Run: `dotnet build`
Expected: Build succeeds.

Run: `dotnet run --project src/API` (with `EventStore:Type` set to `InMemory` in `appsettings.Development.json`, or via Docker Compose for the full Postgres path), then in another terminal:

```bash
curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token from /auth/login>" \
  -d '{"customerId":"<guid>","lines":[{"sku":"<existing sku>","quantity":1}]}'
```

Expected: `202 Accepted` with the new order id. Then `GET /orders/{id}` returns `200 OK` with the read model, and its `status` transitions from `Created` to `Confirmed` (or `Cancelled`) shortly after, once the event chain runs.

- [ ] **Step 4: Commit**

```bash
git add src/API/Orders src/API/Program.cs
git commit -m "feat(ordering): expose PlaceOrder and GetOrderById API endpoints"
```

---

### Task 8: Full verification pass

**Files:** none (verification only).

- [ ] **Step 1: Run the full unit test suite with coverage**

Run: `./scripts/run-unit-tests.sh`
Expected: All tests PASS; `Core`/`Domain`/`Application` coverage stays at or above 75%.

- [ ] **Step 2: Run the full integration test suite**

Run: `./scripts/run-integration-tests.sh`
Expected: All tests PASS, including `PostgresOrderReadModelRepositoryTests`.

- [ ] **Step 3: Build the whole solution one more time**

Run: `dotnet build`
Expected: Build succeeds with no warnings introduced by the new code.

- [ ] **Step 4: Manually verify the end-to-end flow via Docker Compose**

Run: `docker-compose up`, then place an order via `curl` as in Task 7 Step 3 against a SKU that exists with enough stock, and again against one with insufficient stock. Confirm the order's `status` ends up `Confirmed` in the first case and `Cancelled` in the second, and that `GET /inventory/{productId}` shows released stock after the cancellation (reserved count back to what it was before).

- [ ] **Step 5: Commit any fixes found during verification**

If Step 1–4 surface issues, fix them and commit with a message describing the fix (e.g. `fix(ordering): <specific bug>`). If nothing needs fixing, no commit is needed for this task.
