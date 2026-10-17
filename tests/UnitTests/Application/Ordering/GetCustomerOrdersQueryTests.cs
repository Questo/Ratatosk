using Ratatosk.Application.Ordering.Queries;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class GetCustomerOrdersQueryTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task GetCustomerOrders_ReturnsOnlyTheCustomersOrders_NewestFirst_WithLines()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 100);
        var customer = Guid.NewGuid();
        var first = await _host.PlaceOrderAsync(customer, (sku, 1));
        var second = await _host.PlaceOrderAsync(customer, (sku, 2));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 3));
        var third = await _host.PlaceOrderAsync(customer, (sku, 4));

        var result = await _host.SendAsync(new GetCustomerOrdersQuery(customer));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { third, second, first },
            result.Value!.Items.Select(o => o.Id).ToArray()
        );
        Assert.IsTrue(result.Value.Items.All(o => o.CustomerId == customer));
        CollectionAssert.AreEqual(
            new[] { 4, 2, 1 },
            result.Value.Items.Select(o => o.Lines.Single().Quantity).ToArray()
        );
        Assert.AreEqual(3, result.Value.TotalItems);
    }

    [TestMethod]
    public async Task GetCustomerOrders_SecondPage_ReturnsTheRemainingOrdersAndPagingMetadata()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 100);
        var customer = Guid.NewGuid();
        var oldest = await _host.PlaceOrderAsync(customer, (sku, 1));
        await _host.PlaceOrderAsync(customer, (sku, 1));
        await _host.PlaceOrderAsync(customer, (sku, 1));

        var result = await _host.SendAsync(new GetCustomerOrdersQuery(customer, Page: 2, PageSize: 2));

        Assert.IsTrue(result.IsSuccess);
        var page = result.Value!;
        Assert.AreEqual(3, page.TotalItems);
        Assert.AreEqual(2, page.Page);
        Assert.AreEqual(2, page.PageSize);
        Assert.AreEqual(2, page.TotalPages);
        Assert.AreEqual(oldest, page.Items.Single().Id);
    }

    [TestMethod]
    public async Task GetCustomerOrders_ForACustomerWithNoOrders_ReturnsAnEmptyPage()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 1));

        var result = await _host.SendAsync(new GetCustomerOrdersQuery(Guid.NewGuid()));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsEmpty(result.Value!.Items);
        Assert.AreEqual(0, result.Value.TotalItems);
        Assert.AreEqual(0, result.Value.TotalPages);
    }

    [TestMethod]
    public async Task GetCustomerOrders_WithAStatus_ReturnsOnlyTheCustomersOrdersInThatStatus()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 5);
        var customer = Guid.NewGuid();
        await _host.PlaceOrderAsync(customer, (sku, 1));
        var cancelled = await _host.PlaceOrderAsync(customer, (sku, 50));
        await _host.PlaceOrderAsync(Guid.NewGuid(), (sku, 50));

        var result = await _host.SendAsync(new GetCustomerOrdersQuery(customer, Status: "cancelled"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(cancelled, result.Value!.Items.Single().Id);
        Assert.AreEqual(1, result.Value.TotalItems);
    }

    [TestMethod]
    public async Task GetCustomerOrders_WithAnUnknownStatus_Fails()
    {
        var result = await _host.SendAsync(new GetCustomerOrdersQuery(Guid.NewGuid(), Status: "bogus"));

        Assert.IsTrue(result.IsFailure);
        StringAssert.Contains(result.Error, "Valid statuses");
    }

    [TestMethod]
    [DataRow(0, 25)]
    [DataRow(1, 0)]
    [DataRow(1, 101)]
    public async Task GetCustomerOrders_WithAPageOrPageSizeOutOfRange_Fails(int page, int pageSize)
    {
        var result = await _host.SendAsync(new GetCustomerOrdersQuery(Guid.NewGuid(), page, pageSize));

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task GetCustomerOrders_WithTheLargestPageSize_Succeeds()
    {
        var result = await _host.SendAsync(
            new GetCustomerOrdersQuery(Guid.NewGuid(), 1, GetCustomerOrdersQueryHandler.MaxPageSize)
        );

        Assert.IsTrue(result.IsSuccess);
    }
}
