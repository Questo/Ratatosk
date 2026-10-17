using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Inventoring.Queries;

namespace Ratatosk.API.Inventory;

public static class InventoryEndpoints
{
    private const string InventoryTag = "Inventory";

    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/inventory/{productId:guid}/reserve",
                async (
                    Guid productId,
                    StockQuantityRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var command = new ReserveStockCommand(productId, request.Quantity);
                    var result = await dispatcher.DispatchAsync(command, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(InventoryTag)
            .WithName("ReserveStock")
            .WithSummary("Reserve stock for a product")
            .WithDescription("Reserves the requested quantity of a product's stock.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest);

        app.MapPost(
                "/inventory/{productId:guid}/unreserve",
                async (
                    Guid productId,
                    StockQuantityRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var command = new UnreserveStockCommand(productId, request.Quantity);
                    var result = await dispatcher.DispatchAsync(command, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(InventoryTag)
            .WithName("UnreserveStock")
            .WithSummary("Release previously reserved stock for a product")
            .WithDescription("Releases the requested quantity from a product's reserved stock.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest);

        app.MapPost(
                "/inventory/{productId:guid}/restock",
                async (
                    Guid productId,
                    StockQuantityRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var command = new RestockCommand(productId, request.Quantity);
                    var result = await dispatcher.DispatchAsync(command, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(InventoryTag)
            .WithName("RestockProduct")
            .WithSummary("Add stock for a product")
            .WithDescription("Increases a product's available stock by the requested quantity.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest);

        app.MapGet(
                "/inventory/{productId:guid}",
                async (Guid productId, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var query = new GetStockByProductIdQuery(productId);
                    var result = await dispatcher.DispatchAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .WithTags(InventoryTag)
            .WithName("GetStockByProductId")
            .WithSummary("Get stock levels for a product")
            .WithDescription("Returns the current stock read model for the supplied product id.")
            .Produces<StockReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/inventory/by-sku/{sku}",
                async (string sku, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var query = new GetStockBySkuQuery(sku);
                    var result = await dispatcher.DispatchAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .WithTags(InventoryTag)
            .WithName("GetStockBySku")
            .WithSummary("Get stock levels by SKU")
            .WithDescription("Returns the current stock read model for the supplied SKU.")
            .Produces<StockReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record StockQuantityRequest(int Quantity);
