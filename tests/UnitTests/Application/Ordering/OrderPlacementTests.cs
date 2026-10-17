using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Ordering;

// Drives the whole Order reservation process through PlaceOrderCommand: the real dispatcher,
// aggregate repository, event bus and handlers, with in-memory storage.
[TestClass]
public class OrderPlacementTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task PlaceOrder_WithEnoughStock_ConfirmsOrderAndReservesEveryLine()
    {
        var (productA, skuA) = await _host.AddStockedProductAsync(available: 10, price: 19.99m);
        var (productB, skuB) = await _host.AddStockedProductAsync(available: 5);

        var orderId = await _host.PlaceOrderAsync((skuA, 3), (skuB, 5));

        var readModel = await _host.Orders.GetByIdAsync(orderId);
        Assert.IsNotNull(readModel);
        Assert.AreEqual(nameof(OrderStatus.Confirmed), readModel.Status);
        Assert.HasCount(2, readModel.Lines);
        Assert.AreEqual(19.99m, readModel.Lines.Single(l => l.Sku == skuA).UnitPrice);

        Assert.AreEqual(3, (await _host.Stock.GetByProductIdAsync(productA))!.Reserved);
        Assert.AreEqual(5, (await _host.Stock.GetByProductIdAsync(productB))!.Reserved);
    }

    [TestMethod]
    public async Task PlaceOrder_StoresOrderEventsInTheOrderTheyWereRaised()
    {
        var (_, skuA) = await _host.AddStockedProductAsync(available: 10);
        var (_, skuB) = await _host.AddStockedProductAsync(available: 10);

        var orderId = await _host.PlaceOrderAsync((skuA, 1), (skuB, 1));

        var stream = await _host.StreamAsync<Order>(orderId);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(OrderCreated),
                typeof(OrderLineReserved),
                typeof(OrderLineReserved),
                typeof(OrderConfirmed),
            },
            stream.Select(e => e.GetType()).ToArray()
        );
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, stream.Select(e => e.Version).ToArray());
    }

    [TestMethod]
    public async Task PlaceOrder_WhenALineCannotBeReserved_CancelsOrderAndReleasesEarlierLines()
    {
        var (productA, skuA) = await _host.AddStockedProductAsync(available: 10);
        var (productB, skuB) = await _host.AddStockedProductAsync(available: 2);
        var (productC, skuC) = await _host.AddStockedProductAsync(available: 10);

        var orderId = await _host.PlaceOrderAsync((skuA, 4), (skuB, 3), (skuC, 1));

        var order = await _host.LoadAsync<Order>(orderId);
        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
        Assert.AreEqual(nameof(OrderStatus.Cancelled), (await _host.Orders.GetByIdAsync(orderId))!.Status);

        // A was reserved then released; B was rejected; C was never attempted.
        Assert.AreEqual(0, (await _host.Stock.GetByProductIdAsync(productA))!.Reserved);
        Assert.AreEqual(0, (await _host.Stock.GetByProductIdAsync(productB))!.Reserved);
        Assert.AreEqual(0, (await _host.Stock.GetByProductIdAsync(productC))!.Reserved);
        Assert.IsTrue((await _host.LoadAsync<Inventory>(productA)).IsInStock(SkuOf(skuA), 10));

        var rejection = (await _host.StreamAsync<Inventory>(productB))
            .OfType<StockReservationFailed>()
            .Single();
        Assert.AreEqual(orderId, rejection.OrderId);
        Assert.IsEmpty((await _host.StreamAsync<Inventory>(productC)).OfType<StockReserved>());
    }

    [TestMethod]
    public async Task PlaceOrder_WithUnknownSku_IsRejectedBeforeAnOrderExists()
    {
        var result = await _host.SendAsync(
            new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest("NOPE-0000", 1)])
        );

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(0, _host.Orders.SaveCount);
    }

    [TestMethod]
    public async Task Compensation_StillFindsStock_AfterItsProductWasRemovedFromTheCatalog()
    {
        var (productA, skuA) = await _host.AddStockedProductAsync(available: 10);
        var (productB, skuB) = await _host.AddStockedProductAsync(available: 1);
        (await _host.SendAsync(new ReserveStockCommand(productB, 1))).EnsureSuccess();
        (await _host.SendAsync(new RemoveProductCommand(productA))).EnsureSuccess();

        // PlaceOrder would refuse a removed product, so commit the Order directly: line A is
        // reserved, line B is rejected (its last unit is held), and A must then be released.
        var order = Order
            .Place(Guid.NewGuid(), [Line(skuA, quantity: 2), Line(skuB, quantity: 1)])
            .Value!;
        await _host.CommitAsync(order);

        Assert.AreEqual(OrderStatus.Cancelled, (await _host.LoadAsync<Order>(order.Id)).Status);
        Assert.AreEqual(0, (await _host.Stock.GetByProductIdAsync(productA))!.Reserved);
    }

    [TestMethod]
    public async Task PlaceOrder_WhenReservationFailsUnexpectedly_CancelsOrderAndReleasesHeldStock()
    {
        var failing = new FailOnStreamAppend("inventory-", failOnCall: 2);
        using var host = await TestHost.StartAsync(services => failing.Install(services));
        var (productA, skuA) = await host.AddStockedProductAsync(available: 10);
        var (_, skuB) = await host.AddStockedProductAsync(available: 10);
        failing.Arm();

        var orderId = await host.PlaceOrderAsync((skuA, 2), (skuB, 2));

        var order = await host.LoadAsync<Order>(orderId);
        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
        Assert.AreEqual(0, (await host.Stock.GetByProductIdAsync(productA))!.Reserved);
        Assert.IsTrue(
            host.Logs.Entries.Any(e => e.Message.Contains(nameof(OrderReservationProcess))),
            "the failure should be logged, not swallowed"
        );
    }

    [TestMethod]
    public async Task ConcurrentOrders_ForTheLastUnits_ConfirmOneAndCancelTheOther()
    {
        var (productId, sku) = await _host.AddStockedProductAsync(available: 3);

        var first = await _host.PlaceOrderAsync((sku, 2));
        var second = await _host.PlaceOrderAsync((sku, 2));

        Assert.AreEqual(OrderStatus.Confirmed, (await _host.LoadAsync<Order>(first)).Status);
        Assert.AreEqual(OrderStatus.Cancelled, (await _host.LoadAsync<Order>(second)).Status);
        Assert.AreEqual(2, (await _host.Stock.GetByProductIdAsync(productId))!.Reserved);
    }

    private static SKU SkuOf(string sku) => SKU.Create(sku).Value!;

    private static OrderLine Line(string sku, int quantity) =>
        OrderLine.Create(SkuOf(sku), quantity, Price.Create(1m).Value!).Value!;
}

/// <summary>
/// Wraps the in-memory event store and throws on the Nth append to matching streams once armed,
/// simulating an infrastructure failure part-way through a multi-aggregate process.
/// </summary>
public sealed class FailOnStreamAppend(string streamPrefix, int failOnCall) : IEventStore
{
    private IEventStore _inner = null!;
    private bool _armed;
    private int _calls;

    public void Install(IServiceCollection services) =>
        services.AddScoped<IEventStore>(sp =>
        {
            _inner = sp.GetRequiredService<InMemoryEventStore>();
            return this;
        });

    public void Arm() => _armed = true;

    public Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default
    )
    {
        if (_armed && streamName.StartsWith(streamPrefix) && ++_calls == failOnCall)
            throw new IOException("Simulated storage failure");

        return _inner.AppendEventsAsync(streamName, events, expectedVersion, cancellationToken);
    }

    public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int fromVersion = 1,
        CancellationToken cancellationToken = default
    ) => _inner.LoadEventsAsync(streamName, fromVersion, cancellationToken);
}
