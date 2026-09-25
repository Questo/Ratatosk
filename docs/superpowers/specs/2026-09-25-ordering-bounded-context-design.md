# Ordering Bounded Context — Design Spec

Date: 2026-09-25

## Goal

Replace the current placeholder `Order`/`OrderBuilder` stub in `Domain/Ordering` with a real
Ordering bounded context, following the same Domain → Application → Infrastructure → API
layering already used by Catalog and Inventoring. Ordering coordinates stock availability with
Inventoring purely through domain events (no direct service-to-service calls), matching a
microservices-style boundary even though the code lives in one deployable.

See [docs/ORDERING_STOCK_RESERVATION_FLOW.mmd](../ORDERING_STOCK_RESERVATION_FLOW.mmd) for the
end-to-end sequence diagram.

## Domain model (`Domain/Ordering`)

- **`OrderLine`** (value object, `ValueObject` base): `SKU Sku`, `int Quantity`, `Price UnitPrice`
  (price snapshot at order time — reuses the existing `SKU` and `Price` value objects).
- **`OrderStatus`** enum: `Created`, `Confirmed`, `Cancelled`.
- **`Order`** aggregate (`AggregateRoot`):
  - `Guid CustomerId`, `OrderStatus Status`, `IReadOnlyList<OrderLine> Lines`.
  - Private tracking set of SKUs already confirmed-reserved (`HashSet<SKU>`, rebuilt via
    `ApplyEvent`, not exposed) — needed to know when *all* lines are reserved.
  - `static Result<Order> Place(Guid customerId, IEnumerable<OrderLine> lines)` — guards:
    customerId non-empty, lines non-empty, each line quantity > 0. Raises `OrderCreated`.
  - `Result MarkLineReserved(SKU sku)` — valid only from `Created`; ignores unknown/duplicate SKU
    (idempotent). Raises `OrderLineReserved`. If, after applying it, every line's SKU is in the
    reserved set, also raises `OrderConfirmed` in the same call.
  - `Result Cancel(string reason)` — valid only from `Created` (cancelling a `Confirmed` order is
    a future feature, out of scope). Raises `OrderCancelled` (carries `Lines` so the compensation
    handler doesn't need a lookup).
- **Events** (`Domain/Ordering/Events`): `OrderCreated(OrderId, CustomerId, Lines)`,
  `OrderLineReserved(OrderId, SKU)`, `OrderConfirmed(OrderId)`, `OrderCancelled(OrderId, Reason, Lines)`.
- Delete `OrderRenamed.cs` and the placeholder `Rename` behavior — not part of this design.
  `OrderBuilder` gets a real implementation of `IBuilder<Order>` mirroring `InventoryBuilder`
  (used by tests), or is deleted if nothing references it beyond the current `NotImplementedException`
  stub — confirm which during implementation by checking usages.
- No snapshotting for v1 (matches Inventory: don't override `CreateSnapshot()`).

## Inventoring changes (small, additive)

- Extend `StockReserved` with a nullable `Guid? OrderId` (null for manual/ad-hoc reservations via
  the existing `/inventory/{productId}/reserve` endpoint).
- Add new event `StockReservationFailed(InventoryId, SKU, Guid? OrderId, int Quantity, string Reason)`.
- `Inventory.ReserveStock(SKU sku, int quantity, Guid? orderId = null)` — on insufficient stock,
  raise `StockReservationFailed` instead of throwing `InvalidOperationException`. The existing
  manual `ReserveStockCommand` handler must translate a raised `StockReservationFailed` into
  `Result.Failure` (today it presumably catches the thrown exception — check
  `ReserveStockCommandHandler` during implementation and adjust so behavior for the existing
  manual endpoint is unchanged from the caller's point of view).
- `Inventory.ReleaseStock` unchanged — already used for compensation.

## Cross-context event chain

1. `POST /orders` → `OrderService.PlaceOrderAsync` → `Order.Place()` → `OrderCreated`.
2. **`OrderReservationHandler`** (`Application/Inventoring`, `IDomainEventHandler<OrderCreated>`)
   — for each line: load the `Inventory` aggregate for the line's product (by SKU → productId
   lookup, same as existing `GetStockBySkuQuery` path), call `ReserveStock(sku, quantity, orderId)`,
   save, publish whatever event was raised (`StockReserved` or `StockReservationFailed`).
3. **`OrderConfirmationHandler`** (`Application/Ordering`, `IDomainEventHandler<StockReserved>`)
   — no-ops when `OrderId` is null; otherwise loads the `Order` by `OrderId`, calls
   `MarkLineReserved(sku)`, saves, publishes resulting events (`OrderLineReserved`, and
   `OrderConfirmed` once complete).
4. **`OrderCancellationHandler`** (`Application/Ordering`, `IDomainEventHandler<StockReservationFailed>`)
   — no-ops when `OrderId` is null; otherwise loads the `Order`, calls `Cancel(reason)`, saves,
   publishes `OrderCancelled`.
5. **`ReservationCompensationHandler`** (`Application/Inventoring`, `IDomainEventHandler<OrderCancelled>`)
   — for each line in the cancelled order, best-effort `ReleaseStock(sku, quantity)` on the
   relevant `Inventory` (releasing a line that was never actually reserved is a safe no-op —
   `ReleaseStock` already guards against releasing more than is reserved; wrap per-line in a
   try/catch so one failure doesn't stop releasing the rest).

All five handlers are picked up automatically by the existing reflection-based
`AddProjections()`/`AddRequestHandlers()` scan in `Application/DependencyInjection.cs` — no new
manual DI wiring needed beyond registering `IOrderReadModelRepository` and any new domain
service in `Infrastructure/DependencyInjection.cs` (mirroring the Inventoring block).

## Application layer (`Application/Ordering`)

- `Commands/PlaceOrderCommand(CustomerId, IEnumerable<OrderLineRequest>)`, handled by a
  `PlaceOrderCommandHandler` that resolves `SKU` → `Price` via the Catalog read model
  (`IProductReadModelRepository`) to build `OrderLine`s with a price snapshot, then calls
  `Order.Place()` and saves.
- `Queries/GetOrderByIdQuery(OrderId)` against a new `IOrderReadModelRepository`.
- `IOrderService` / `OrderService` facade (mirrors `IInventoryService`/`InventoryService`):
  `PlaceOrderAsync`, `GetOrderByIdAsync`.
- `Models/OrderReadModel` (order id, customer id, status, lines, timestamps) and
  `OrderLineReadModel`.
- **`OrderProjection`** (`Application/Ordering`) — `IDomainEventHandler<OrderCreated>`,
  `IDomainEventHandler<OrderConfirmed>`, `IDomainEventHandler<OrderCancelled>` — maintains the
  read model status only (line-by-line reservation progress is not surfaced in the read model
  for v1).

## Infrastructure

- `Infrastructure/Persistence/ReadModels/OrderReadModel.cs` — `OrderReadModelRepository` on
  `PostgresRepository`, same `INSERT ... ON CONFLICT` pattern as `InventoryReadModelRepository`.
- New tables in `postgres/init/01-init.sql`: `order_read_models` (order_id PK, customer_id,
  status, created_utc, updated_utc) and `order_line_read_models` (order_id FK, sku, quantity,
  unit_price, unit_price_currency) — mirrors how Inventoring's schema is defined inline.
- Register `IOrderReadModelRepository` in `Infrastructure/DependencyInjection.cs`.
- `JsonPolymorphicSerializer`: add a converter for `OrderLine` only if it needs custom handling
  beyond what `SKU`/`Price`'s existing converters already provide (check during implementation —
  likely no new converter needed since `OrderLine` is composed of already-convertible types).

## API (`API/Orders/OrderEndpoints.cs`)

- `POST /orders` — body `{ customerId, lines: [{ sku, quantity }] }` → `PlaceOrderCommand`.
  Returns `202 Accepted` with the order id (placement is async — confirmation happens via the
  event chain, not synchronously in the response), not `200 Ok` like the synchronous Inventoring
  endpoints.
- `GET /orders/{orderId:guid}` — returns `OrderReadModel` or `404`.
- Both `RequireAuthorization(Policies.Authenticated)`, following `InventoryEndpoints`'s
  `.WithTags`/`.WithSummary`/`.Produces<>` conventions. Register `app.MapOrderEndpoints()` in
  `Program.cs` alongside the other `Map*Endpoints()` calls.

## Testing

- `tests/UnitTests/Domain/Ordering/OrderTests.cs` — MSTest, arrange-act-assert against the
  aggregate, asserting on `UncommittedEvents` (mirrors `InventoryTests.cs`): placing an order,
  marking lines reserved one at a time (confirms only after the last one), cancelling.
- `tests/UnitTests/Application/Ordering/*.cs` — `OrderServiceTests`, `OrderProjectionTests`,
  and one test class per new handler (`OrderReservationHandlerTests`,
  `OrderConfirmationHandlerTests`, `OrderCancellationHandlerTests`,
  `ReservationCompensationHandlerTests`), mocking repositories/event bus with Moq, mirroring
  `InventoryProvisioningHandlerTests.cs`.
- `tests/IntegrationTests/PostgresOrderReadModelRepositoryTests.cs` — mirrors
  `PostgresInventoryReadModelRepositoryTests.cs`.
- Coverage must keep `Core`/`Domain`/`Application` at or above the existing 75% threshold
  (`./scripts/run-unit-tests.sh`).

## Known gaps / explicitly out of scope for v1

- Cancelling a `Confirmed` order (post-fulfillment cancellation/refund flow).
- Partial fulfillment or backorder handling — a line either reserves fully or the whole order
  is cancelled.
- Order read model does not expose per-line reservation progress, only final status.
- No new Customer aggregate — `CustomerId` is an opaque `Guid` (expected to be an Identity
  `User.Id`), with no cross-context validation that it exists.
