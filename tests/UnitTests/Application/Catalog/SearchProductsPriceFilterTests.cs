using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class SearchProductsPriceFilterTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _host = await TestHost.StartAsync();

        var now = DateTime.UtcNow;
        await SeedAsync("Acorn", 5m, now.AddMinutes(-5));
        await SeedAsync("Hazelnut", 10m, now.AddMinutes(-4));
        await SeedAsync("Walnut", 20m, now.AddMinutes(-3));
        await SeedAsync("Acorn Deluxe", 30m, now.AddMinutes(-2));
        await SeedAsync("Acorn Supreme", 40m, now.AddMinutes(-1));
    }

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task MinPriceOnly_ReturnsProductsAtOrAboveTheBound()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MinPrice: 20m));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEquivalent(
            new[] { "Walnut", "Acorn Deluxe", "Acorn Supreme" },
            Names(result.Value!)
        );
        Assert.AreEqual(3, result.Value!.TotalItems);
    }

    [TestMethod]
    public async Task MaxPriceOnly_ReturnsProductsAtOrBelowTheBound()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MaxPrice: 10m));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEquivalent(new[] { "Acorn", "Hazelnut" }, Names(result.Value!));
        Assert.AreEqual(2, result.Value!.TotalItems);
    }

    [TestMethod]
    public async Task BothBounds_AreInclusive()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MinPrice: 10m, MaxPrice: 30m));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEquivalent(
            new[] { "Hazelnut", "Walnut", "Acorn Deluxe" },
            Names(result.Value!)
        );
        Assert.AreEqual(3, result.Value!.TotalItems);
    }

    [TestMethod]
    public async Task EqualBounds_ReturnProductsAtExactlyThatPrice()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MinPrice: 20m, MaxPrice: 20m));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(new[] { "Walnut" }, Names(result.Value!));
        Assert.AreEqual(1, result.Value!.TotalItems);
    }

    [TestMethod]
    public async Task PriceRange_CombinesWithSearchTermAndPaging()
    {
        // "Acorn" matches 5, 30 and 40; the range drops the 5, leaving two, newest first.
        var firstPage = await _host.SendAsync(
            new SearchProductsQuery("Acorn", Page: 1, PageSize: 1, MinPrice: 6m, MaxPrice: 40m)
        );
        var secondPage = await _host.SendAsync(
            new SearchProductsQuery("Acorn", Page: 2, PageSize: 1, MinPrice: 6m, MaxPrice: 40m)
        );

        Assert.IsTrue(firstPage.IsSuccess);
        CollectionAssert.AreEqual(new[] { "Acorn Supreme" }, Names(firstPage.Value!));
        Assert.AreEqual(2, firstPage.Value!.TotalItems);
        Assert.IsTrue(secondPage.IsSuccess);
        CollectionAssert.AreEqual(new[] { "Acorn Deluxe" }, Names(secondPage.Value!));
        Assert.AreEqual(2, secondPage.Value!.TotalItems);
    }

    [TestMethod]
    public async Task NegativeMinPrice_Fails()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MinPrice: -1m, MaxPrice: -2m));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("minPrice cannot be negative", result.Error);
    }

    [TestMethod]
    public async Task NegativeMaxPrice_Fails()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MaxPrice: -1m));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("maxPrice cannot be negative", result.Error);
    }

    [TestMethod]
    public async Task MinPriceGreaterThanMaxPrice_Fails()
    {
        var result = await _host.SendAsync(new SearchProductsQuery(MinPrice: 50m, MaxPrice: 10m));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("minPrice cannot be greater than maxPrice", result.Error);
    }

    private Task SeedAsync(string name, decimal price, DateTime lastUpdatedUtc) =>
        _host.Products.SaveAsync(
            new ProductReadModel(
                Guid.NewGuid(),
                name,
                $"SKU-{name.Replace(' ', '-')}",
                $"{name} description",
                price,
                lastUpdatedUtc
            )
        );

    private static string[] Names(Pagination<ProductReadModel> page) =>
        [.. page.Items.Select(p => p.Name)];
}
