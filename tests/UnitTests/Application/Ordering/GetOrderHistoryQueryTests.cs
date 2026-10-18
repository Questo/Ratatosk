using Ratatosk.Application.Ordering.Queries;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class GetOrderHistoryQueryTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task GetOrderHistory_ForAConfirmedOrder_ReturnsTheOrderAfterEachEventInOrder()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        var orderId = await _host.PlaceOrderAsync((sku, 2));

        var result = await _host.SendAsync(new GetOrderHistoryQuery(orderId));

        Assert.IsTrue(result.IsSuccess);
        var entries = result.Value!;
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, entries.Select(e => e.Version).ToArray());
        CollectionAssert.AreEqual(
            new[] { nameof(OrderCreated), nameof(OrderLineReserved), nameof(OrderConfirmed) },
            entries.Select(e => e.Event).ToArray()
        );

        Assert.AreEqual(nameof(OrderStatus.Placed), entries[0].Order.Status);
        Assert.AreEqual(orderId, entries[0].Order.Id);
        Assert.AreEqual(sku, entries[0].Order.Lines.Single().Sku);
        Assert.IsEmpty(entries[0].ReservedSkus);

        Assert.AreEqual(nameof(OrderStatus.Placed), entries[1].Order.Status);
        CollectionAssert.AreEqual(new[] { sku }, entries[1].ReservedSkus.ToArray());

        Assert.AreEqual(nameof(OrderStatus.Confirmed), entries[2].Order.Status);
        CollectionAssert.AreEqual(new[] { sku }, entries[2].ReservedSkus.ToArray());

        var stream = await _host.StreamAsync<Order>(orderId);
        CollectionAssert.AreEqual(
            stream.Select(e => e.OccurredAtUtc).ToArray(),
            entries.Select(e => e.OccurredAtUtc).ToArray()
        );
    }

    [TestMethod]
    public async Task GetOrderHistory_ForACancelledOrder_EndsWithTheCancellationReason()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 1);
        var orderId = await _host.PlaceOrderAsync((sku, 2));

        var result = await _host.SendAsync(new GetOrderHistoryQuery(orderId));

        Assert.IsTrue(result.IsSuccess);
        var last = result.Value![^1];
        Assert.AreEqual(nameof(OrderCancelled), last.Event);
        Assert.AreEqual(nameof(OrderStatus.Cancelled), last.Order.Status);
        Assert.AreEqual($"Not enough stock available for SKU {sku}", last.Order.CancellationReason);
        Assert.IsNull(result.Value[0].Order.CancellationReason);
    }

    [TestMethod]
    public async Task GetOrderHistory_ForAnUnknownOrder_Fails()
    {
        var orderId = Guid.NewGuid();

        var result = await _host.SendAsync(new GetOrderHistoryQuery(orderId));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"No order found with id {orderId}", result.Error);
    }
}
