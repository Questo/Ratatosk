using Ratatosk.Application.Ordering.Queries;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class GetOrdersQueryTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task GetOrders_WithoutFilters_ReturnsEveryCustomersOrders_NewestFirst_WithLines()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 100);
        var first = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));
        var second = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 2));
        var third = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 3));

        var result = await _host.SendAsync(new GetOrdersQuery());

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { third, second, first },
            result.Value!.Items.Select(o => o.Id).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { 3, 2, 1 },
            result.Value.Items.Select(o => o.Lines.Single().Quantity).ToArray()
        );
        Assert.AreEqual(3, result.Value.Items.Select(o => o.CustomerId).Distinct().Count());
        Assert.AreEqual(3, result.Value.TotalItems);
    }

    [TestMethod]
    public async Task GetOrders_WithACustomerId_ReturnsOnlyThatCustomersOrders()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 100);
        var customer = Guid.NewGuid();
        var first = await _host.PlaceOrderAsync(customer, (sku, 1));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));
        var second = await _host.PlaceOrderAsync(customer, (sku, 1));

        var result = await _host.SendAsync(new GetOrdersQuery(CustomerId: customer));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { second, first },
            result.Value!.Items.Select(o => o.Id).ToArray()
        );
        Assert.AreEqual(2, result.Value.TotalItems);
    }

    [TestMethod]
    [DataRow("Cancelled")]
    [DataRow("cancelled")]
    [DataRow("CANCELLED")]
    public async Task GetOrders_WithAStatus_MatchesItIgnoringCase(string status)
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 5);
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));
        var cancelled = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 50));

        var result = await _host.SendAsync(new GetOrdersQuery(Status: status));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(cancelled, result.Value!.Items.Single().Id);
        Assert.AreEqual("Cancelled", result.Value.Items.Single().Status);
        Assert.AreEqual(1, result.Value.TotalItems);
    }

    [TestMethod]
    public async Task GetOrders_WithACustomerIdAndAStatus_AppliesBoth()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 5);
        var customer = Guid.NewGuid();
        var confirmed = await _host.PlaceOrderAsync(customer, (sku, 1));
        await _host.PlaceOrderAsync(customer, (sku, 50));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));

        var result = await _host.SendAsync(new GetOrdersQuery(customer, "confirmed"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(confirmed, result.Value!.Items.Single().Id);
        Assert.AreEqual(1, result.Value.TotalItems);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("1")]
    [DataRow("")]
    public async Task GetOrders_WithAnUnknownStatus_FailsListingTheValidNames(string status)
    {
        var result = await _host.SendAsync(new GetOrdersQuery(Status: status));

        Assert.IsTrue(result.IsFailure);
        foreach (var name in Enum.GetNames<Ratatosk.Domain.Ordering.OrderStatus>())
            StringAssert.Contains(result.Error, name);
    }

    [TestMethod]
    public async Task GetOrders_SecondPage_ReturnsTheRemainingOrdersAndPagingMetadata()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 100);
        var oldest = await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));

        var result = await _host.SendAsync(new GetOrdersQuery(Page: 2, PageSize: 2));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.Value!.TotalItems);
        Assert.AreEqual(2, result.Value.TotalPages);
        Assert.AreEqual(oldest, result.Value.Items.Single().Id);
    }

    [TestMethod]
    [DataRow(0, 25)]
    [DataRow(1, 0)]
    [DataRow(1, 101)]
    public async Task GetOrders_WithAPageOrPageSizeOutOfRange_Fails(int page, int pageSize)
    {
        var result = await _host.SendAsync(new GetOrdersQuery(Page: page, PageSize: pageSize));

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task GetOrders_WithTheLargestPageSize_Succeeds()
    {
        var result = await _host.SendAsync(new GetOrdersQuery(PageSize: GetOrdersQueryHandler.MaxPageSize));

        Assert.IsTrue(result.IsSuccess);
    }
}
