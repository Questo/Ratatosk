using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Shared;

public static class TestHostExtensions
{
    /// <summary>Adds a product (which provisions its Inventory) and restocks it.</summary>
    public static async Task<(Guid ProductId, string Sku)> AddStockedProductAsync(
        this TestHost host,
        int available,
        decimal price = 10m
    )
    {
        var sku = SkuGenerator.Generate("TS");
        var added = await host.SendAsync(
            new AddProductCommand("Test Product", sku, "A product for testing", price)
        );
        added.EnsureSuccess();

        if (available > 0)
            (await host.SendAsync(new RestockCommand(added.Value, available))).EnsureSuccess();

        return (added.Value, sku);
    }

    public static async Task<Guid> PlaceOrderAsync(
        this TestHost host,
        params (string Sku, int Quantity)[] lines
    )
    {
        var placed = await host.SendAsync(
            new PlaceOrderCommand(Guid.NewGuid(), [.. lines.Select(l => new OrderLineRequest(l.Sku, l.Quantity))])
        );
        placed.EnsureSuccess();
        return placed.Value;
    }
}
