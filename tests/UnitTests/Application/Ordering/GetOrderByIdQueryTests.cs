using Ratatosk.Application.Ordering.Queries;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class GetOrderByIdQueryTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task GetOrderById_ForTheCustomerWhoPlacedIt_ReturnsTheOrder()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        var customer = Guid.NewGuid();
        var orderId = await _host.PlaceOrderAsync(customer, (sku, 1));

        var result = await _host.SendAsync(new GetOrderByIdQuery(orderId, customer, CallerIsAdmin: false));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(orderId, result.Value!.Id);
        Assert.AreEqual(customer, result.Value.CustomerId);
    }

    [TestMethod]
    public async Task GetOrderById_ForAnotherCustomer_FailsAsIfTheOrderDidNotExist()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        var orderId = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));

        var result = await _host.SendAsync(new GetOrderByIdQuery(orderId, Guid.NewGuid(), CallerIsAdmin: false));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"No order found with id {orderId}", result.Error);
    }

    [TestMethod]
    public async Task GetOrderById_ForAnAdminWhoDidNotPlaceIt_ReturnsTheOrder()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        var customer = Guid.NewGuid();
        var orderId = await _host.PlaceOrderAsync(customer, (sku, 1));

        var result = await _host.SendAsync(new GetOrderByIdQuery(orderId, Guid.NewGuid(), CallerIsAdmin: true));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(orderId, result.Value!.Id);
        Assert.AreEqual(customer, result.Value.CustomerId);
    }

    [TestMethod]
    public async Task GetOrderById_ForAnUnknownOrder_FailsEvenForAnAdmin()
    {
        var orderId = Guid.NewGuid();

        var result = await _host.SendAsync(new GetOrderByIdQuery(orderId, Guid.NewGuid(), CallerIsAdmin: true));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"No order found with id {orderId}", result.Error);
    }
}
