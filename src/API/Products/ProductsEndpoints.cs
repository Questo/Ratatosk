using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;

namespace Ratatosk.API.Products;

public static class ProductsEndpoints
{
    public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (
            AddProductCommand cmd,
            ICatalogService catalogService,
            CancellationToken ct) =>
        {
            var result = await catalogService.AddProductAsync(cmd, ct);

            return result.IsFailure
                ? Results.BadRequest(result.Error)
                : Results.Created();

            //: Results.Created($"/products/{result.Value}", result.Value);
        });
    }
}