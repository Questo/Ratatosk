using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Search;
using Ratatosk.Domain.Catalog;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Search;

[TestClass]
public class SearchQueryTests
{
    private const string TermError = "Search term must be between 2 and 100 characters.";

    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" a ")]
    public async Task Search_WithATooShortTerm_Fails(string? term)
    {
        var result = await _host.SendAsync(new SearchQuery(term));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(TermError, result.Error);
    }

    [TestMethod]
    public async Task Search_WithA101CharacterTerm_Fails()
    {
        var result = await _host.SendAsync(new SearchQuery(new string('x', 101)));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(TermError, result.Error);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(100)]
    public async Task Search_WithATermAtTheBounds_Succeeds(int length)
    {
        var result = await _host.SendAsync(new SearchQuery("  " + new string('x', length) + "  "));

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(50, 10)]
    [DataRow(3, 3)]
    public async Task Search_ClampsTheLimit(int limit, int expected)
    {
        for (var i = 0; i < 12; i++)
            await AddProductAsync($"Gadget {i}", SkuGenerator.Generate("GD"), "A plain product");

        var result = await _host.SendAsync(new SearchQuery("gadget", limit));

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(expected, Group(result.Value!, "product").Hits);
    }

    [TestMethod]
    public async Task Search_ReturnsProductsThenOrders_EvenWithoutHits()
    {
        var result = await _host.SendAsync(new SearchQuery("nothing-matches"));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "product", "order" },
            result.Value!.Groups.Select(g => g.Kind).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "Products", "Orders" },
            result.Value.Groups.Select(g => g.Label).ToArray()
        );
        Assert.IsTrue(result.Value.Groups.All(g => g.Hits.Count == 0 && !g.Failed));
    }

    [TestMethod]
    public async Task Search_FindsProductsByNameSkuAndDescription_WithTheSubtitleFormat()
    {
        var byName = await AddProductAsync("Search Widget", "SW-000001", "A plain product", 12.5m);
        var bySku = await AddProductAsync("Other", "QQ-WIDGET", "A plain product", 3m);
        var byDescription = await AddProductAsync("Third", "TT-000003", "A holder for any widget", 7m);
        await AddProductAsync("Unrelated", "UN-000004", "A plain product", 1m);

        var result = await _host.SendAsync(new SearchQuery("WIDGET"));

        var hits = Group(result.Value!, "product").Hits;
        CollectionAssert.AreEquivalent(
            new[] { byName, bySku, byDescription },
            hits.Select(h => h.Id).ToArray()
        );
        var widget = hits.Single(h => h.Id == byName);
        Assert.AreEqual("Search Widget", widget.Title);
        Assert.AreEqual("SW-000001 · 12.50", widget.Subtitle);
        Assert.AreEqual("product", widget.Kind);
    }

    [TestMethod]
    public async Task Search_FindsOrdersByPartialIdCustomerIdStatusAndLineSku_WithTheTitleAndSubtitleFormat()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 5);
        var (_, otherSku) = await _host.AddStockedProductAsync(onHand: 5);
        var customer = Guid.NewGuid();
        var confirmed = await _host.PlaceOrderAsync(customer, (sku, 1));
        var cancelled = await _host.PlaceOrderAsync((otherSku, 2), (sku, 50));

        Assert.AreEqual(confirmed, OrderHits(await SearchAsync(confirmed.ToString()[..6])).Single().Id);
        Assert.AreEqual(confirmed, OrderHits(await SearchAsync(customer.ToString().ToUpperInvariant())).Single().Id);
        Assert.AreEqual(confirmed, OrderHits(await SearchAsync("CONFIRMED")).Single().Id);
        Assert.AreEqual(cancelled, OrderHits(await SearchAsync("cancel")).Single().Id);
        Assert.AreEqual(cancelled, OrderHits(await SearchAsync(otherSku.ToLowerInvariant())).Single().Id);
        CollectionAssert.AreEqual(
            new[] { cancelled, confirmed },
            OrderHits(await SearchAsync(sku)).Select(h => h.Id).ToArray()
        );

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var confirmedHit = OrderHits(await SearchAsync("confirmed")).Single();
        Assert.AreEqual(confirmed.ToString("D")[..8], confirmedHit.Title);
        Assert.AreEqual($"Confirmed · 1 line · {today}", confirmedHit.Subtitle);
        Assert.AreEqual("order", confirmedHit.Kind);
        Assert.AreEqual($"Cancelled · 2 lines · {today}", OrderHits(await SearchAsync("cancelled")).Single().Subtitle);
    }

    [TestMethod]
    public async Task Search_WhenAProviderThrows_FlagsItsGroupAndStillReturnsTheOthers()
    {
        using var host = await TestHost.StartAsync(services =>
            services.AddScoped<ISearchProvider, ThrowingSearchProvider>()
        );
        var (_, sku) = await host.AddStockedProductAsync(onHand: 5);
        var order = await host.PlaceOrderAsync((sku, 1));

        var result = await host.SendAsync(new SearchQuery(sku));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "product", "broken", "order" },
            result.Value!.Groups.Select(g => g.Kind).ToArray()
        );
        var broken = Group(result.Value, "broken");
        Assert.IsTrue(broken.Failed);
        Assert.IsEmpty(broken.Hits);
        Assert.AreEqual("Broken", broken.Label);
        Assert.IsFalse(Group(result.Value, "product").Failed);
        Assert.HasCount(1, Group(result.Value, "product").Hits);
        Assert.AreEqual(order, Group(result.Value, "order").Hits.Single().Id);
        Assert.IsTrue(host.Logs.Entries.Any(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException));
    }

    private async Task<SearchResults> SearchAsync(string term)
    {
        var result = await _host.SendAsync(new SearchQuery(term));
        Assert.IsTrue(result.IsSuccess);
        return result.Value!;
    }

    private static SearchGroup Group(SearchResults results, string kind) =>
        results.Groups.Single(g => g.Kind == kind);

    private static IReadOnlyList<SearchHit> OrderHits(SearchResults results) => Group(results, "order").Hits;

    private async Task<Guid> AddProductAsync(string name, string sku, string description, decimal price = 10m)
    {
        var added = await _host.SendAsync(new AddProductCommand(name, sku, description, price));
        added.EnsureSuccess();
        return added.Value;
    }

    private sealed class ThrowingSearchProvider : ISearchProvider
    {
        public string Kind => "broken";
        public string Label => "Broken";
        public int Order => 15;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            string term,
            int limit,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Search backend down");
    }
}
