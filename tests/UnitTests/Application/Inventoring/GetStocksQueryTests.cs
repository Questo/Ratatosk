using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Inventoring.Queries;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class GetStocksQueryTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task GetStocks_ReturnsEveryRecord_OrderedBySku_WithOnHandReservedAndAvailable()
    {
        var (first, _) = await _host.AddStockedProductAsync(onHand: 10);
        var (second, _) = await _host.AddStockedProductAsync(onHand: 4);
        var (third, _) = await _host.AddStockedProductAsync(onHand: 0);
        (await _host.SendAsync(new ReserveStockCommand(first, 3))).EnsureSuccess();

        var result = await _host.SendAsync(new GetStocksQuery());

        Assert.IsTrue(result.IsSuccess);
        var items = result.Value!.Items;
        Assert.AreEqual(3, result.Value.TotalItems);
        CollectionAssert.AreEquivalent(
            new[] { first, second, third },
            items.Select(s => s.ProductId).ToArray()
        );
        CollectionAssert.AreEqual(
            items.Select(s => s.Sku).Order(StringComparer.Ordinal).ToArray(),
            items.Select(s => s.Sku).ToArray()
        );
        var reserved = items.Single(s => s.ProductId == first);
        Assert.AreEqual(10, reserved.OnHand);
        Assert.AreEqual(3, reserved.Reserved);
        Assert.AreEqual(7, reserved.Available);
        Assert.AreEqual(4, items.Single(s => s.ProductId == second).Available);
        Assert.AreEqual(0, items.Single(s => s.ProductId == third).OnHand);
    }

    [TestMethod]
    public async Task GetStocks_SecondPage_ReturnsTheNextRecordAndPagingMetadata()
    {
        await _host.AddStockedProductAsync(onHand: 1);
        await _host.AddStockedProductAsync(onHand: 1);
        await _host.AddStockedProductAsync(onHand: 1);
        var all = (await _host.SendAsync(new GetStocksQuery())).Value!.Items;

        var result = await _host.SendAsync(new GetStocksQuery(Page: 2, PageSize: 1));

        Assert.IsTrue(result.IsSuccess);
        var page = result.Value!;
        Assert.AreEqual(all[1].ProductId, page.Items.Single().ProductId);
        Assert.AreEqual(3, page.TotalItems);
        Assert.AreEqual(2, page.Page);
        Assert.AreEqual(1, page.PageSize);
        Assert.AreEqual(3, page.TotalPages);
    }

    [TestMethod]
    [DataRow(0, 25)]
    [DataRow(1, 0)]
    [DataRow(1, 101)]
    public async Task GetStocks_WithAPageOrPageSizeOutOfRange_Fails(int page, int pageSize)
    {
        var result = await _host.SendAsync(new GetStocksQuery(page, pageSize));

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task GetStocks_WithTheLargestPageSize_Succeeds()
    {
        var result = await _host.SendAsync(new GetStocksQuery(1, GetStocksQueryHandler.MaxPageSize));

        Assert.IsTrue(result.IsSuccess);
    }
}
