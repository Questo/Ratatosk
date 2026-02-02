using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Queries;

namespace Ratatosk.API.Products;

public static class ProductsEndpoints
{
    public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/products",
                async (
                    AddProductCommand cmd,
                    ICatalogService catalogService,
                    CancellationToken ct
                ) =>
                {
                    var result = await catalogService.AddProductAsync(cmd, ct);
                    var response = Response<Guid>.FromResult(result);

                    return result.IsFailure
                        ? Results.BadRequest(response)
                        : Results.Created($"/products/{result.Value}", response);
                }
            )
            .RequireAuthorization();

        app.MapGet(
                "/products/{id:guid}",
                async (Guid id, ICatalogService catalogService, CancellationToken ct) =>
                {
                    var query = new GetProductByIdQuery(id);
                    var result = await catalogService.GetProductByIdAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization();

        app.MapPut(
                "/products/{id:guid}",
                async (
                    Guid id,
                    UpdateProductCommand cmd,
                    ICatalogService service,
                    CancellationToken ct
                ) =>
                {
                    var result = await service.UpdateProductAsync(cmd with { ProductId = id }, ct);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
                }
            )
            .RequireAuthorization();

        app.MapGet(
                "/products",
                async (
                    [AsParameters] SearchProductsRequest request,
                    ICatalogService catalogService,
                    CancellationToken ct
                ) =>
                {
                    var query = new SearchProductsQuery(
                        request.SearchTerm,
                        request.Page,
                        request.PageSize
                    );
                    var result = await catalogService.GetProductsAsync(query, ct);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization();

        app.MapDelete(
                "/products/{id:guid}",
                async (Guid id, ICatalogService catalogService, CancellationToken ct) =>
                {
                    var command = new RemoveProductCommand(id);
                    var result = await catalogService.RemoveProductAsync(command, ct);

                    return result.IsFailure ? Results.NotFound() : Results.NoContent();
                }
            )
            .RequireAuthorization();
    }
}
