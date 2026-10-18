using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Shared;
using System.Security.Claims;

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
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var result = await dispatcher.DispatchAsync(cmd, ct);
                    var response = Response<Guid>.FromResult(result);

                    return result.IsFailure
                        ? Results.BadRequest(response)
                        : Results.Created($"/products/{result.Value}", response);
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(ProductsTag)
            .WithName("CreateProduct")
            .WithSummary("Create a new product")
            .WithDescription(
                "Creates a product from the posted payload and returns its identifier."
            )
            .Accepts<AddProductCommand>("application/json")
            .Produces<Response<Guid>>(StatusCodes.Status201Created)
            .Produces<Response<Guid>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet(
                "/products/{id:guid}",
                async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var query = new GetProductByIdQuery(id);
                    var result = await dispatcher.DispatchAsync(query, ct);
                    var response = Response<ProductReadModel>.FromResult(result);

                    return result.IsFailure ? Results.NotFound(response) : Results.Ok(response);
                }
            )
            // .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("GetProductById")
            .WithSummary("Get a product by id")
            .WithDescription("Returns a product read model for the supplied product id.")
            .Produces<Response<ProductReadModel>>(StatusCodes.Status200OK)
            .Produces<Response<ProductReadModel>>(StatusCodes.Status404NotFound);

        app.MapPut(
                "/products/{id:guid}",
                async (
                    Guid id,
                    UpdateProductCommand cmd,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var result = await dispatcher.DispatchAsync(cmd with { ProductId = id }, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(ProductsTag)
            .WithName("UpdateProduct")
            .WithSummary("Update an existing product")
            .WithDescription("Updates a product by id using the supplied payload.")
            .Accepts<UpdateProductCommand>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet(
                "/products",
                async (
                    [AsParameters] SearchProductsRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var query = new SearchProductsQuery(
                        request.SearchTerm,
                        request.Page,
                        request.PageSize,
                        request.MinPrice,
                        request.MaxPrice
                    );
                    var result = await dispatcher.DispatchAsync(query, ct);
                    var response = Response<Pagination<ProductReadModel>>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            // .RequireAuthorization()
            .WithTags(ProductsTag)
            .WithName("SearchProducts")
            .WithSummary("Search products")
            .WithDescription(
                "Returns paged products filtered by an optional search term and optional inclusive minPrice/maxPrice bounds. Paging defaults to page 1, size 25."
            )
            .Produces<Response<Pagination<ProductReadModel>>>(StatusCodes.Status200OK)
            .Produces<Response<Pagination<ProductReadModel>>>(StatusCodes.Status400BadRequest);

        app.MapDelete(
                "/products/{id:guid}",
                async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var command = new RemoveProductCommand(id);
                    var result = await dispatcher.DispatchAsync(command, ct);

                    return result.IsFailure ? Results.NotFound() : Results.NoContent();
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(ProductsTag)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product")
            .WithDescription("Deletes the product by id. Returns 404 if it does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Example: requires a valid token AND the Admin role
        app.MapGet(
                "/products/admin/summary",
                (HttpContext ctx) =>
                {
                    var email = ctx.User.FindFirstValue(ClaimTypes.Email);
                    var role = ctx.User.FindFirstValue(ClaimTypes.Role);
                    return Results.Ok(new { email, role, message = "Admin-only endpoint." });
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(ProductsTag)
            .WithName("AdminProductSummary")
            .WithSummary("Admin-only summary")
            .WithDescription("Example endpoint requiring both a valid JWT and the Admin role.")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
