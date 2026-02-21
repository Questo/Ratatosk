using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;

namespace Ratatosk.API.Products;

public static class ProductsEndpoints
{
    private const string ProductsTag = "Products";

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
            //.RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("CreateProduct")
            .WithSummary("Create a new product")
            .WithDescription(
                "Creates a product from the posted payload and returns its identifier."
            )
            .Accepts<AddProductCommand>("application/json")
            .Produces<Response<Guid>>(StatusCodes.Status201Created)
            .Produces<Response<Guid>>(StatusCodes.Status400BadRequest);

        app.MapGet(
                "/products/{id:guid}",
                async (Guid id, ICatalogService catalogService, CancellationToken ct) =>
                {
                    var query = new GetProductByIdQuery(id);
                    var result = await catalogService.GetProductByIdAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            // .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("GetProductById")
            .WithSummary("Get a product by id")
            .WithDescription("Returns a product read model for the supplied product id.")
            .Produces<ProductReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

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
            // .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("UpdateProduct")
            .WithSummary("Update an existing product")
            .WithDescription("Updates a product by id using the supplied payload.")
            .Accepts<UpdateProductCommand>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest);

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
            // .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("SearchProducts")
            .WithSummary("Search products")
            .WithDescription(
                "Returns paged products filtered by an optional search term. Paging defaults to page 1, size 25."
            )
            .Produces<Pagination<ProductReadModel>>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest);

        app.MapDelete(
                "/products/{id:guid}",
                async (Guid id, ICatalogService catalogService, CancellationToken ct) =>
                {
                    var command = new RemoveProductCommand(id);
                    var result = await catalogService.RemoveProductAsync(command, ct);

                    return result.IsFailure ? Results.NotFound() : Results.NoContent();
                }
            )
            .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product")
            .WithDescription("Deletes the product by id. Returns 404 if it does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
