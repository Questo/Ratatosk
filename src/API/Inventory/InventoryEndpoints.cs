using Microsoft.AspNetCore.Mvc;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Inventoring.Queries;
using Ratatosk.Application.Shared;

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
            .RequireAuthorization(Policies.Admin)
            .WithTags(InventoryTag)
            .WithName("ReserveStock")
            .WithSummary("Reserve stock for a product")
            .WithDescription("Reserves the requested quantity of a product's stock.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost(
                "/inventory/{productId:guid}/release",
                async (
                    Guid productId,
                    StockQuantityRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var command = new ReleaseStockCommand(productId, request.Quantity);
                    var result = await dispatcher.DispatchAsync(command, ct);
                    var response = Response.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(InventoryTag)
            .WithName("ReleaseStock")
            .WithSummary("Release previously reserved stock for a product")
            .WithDescription("Releases the requested quantity from a product's reserved stock.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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
            .RequireAuthorization(Policies.Admin)
            .WithTags(InventoryTag)
            .WithName("RestockProduct")
            .WithSummary("Add stock for a product")
            .WithDescription("Increases a product's available stock by the requested quantity.")
            .Accepts<StockQuantityRequest>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet(
                "/inventory",
                async (
                    [AsParameters] ListStockRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var query = new GetStocksQuery(request.Page, request.PageSize);
                    var result = await dispatcher.DispatchAsync(query, ct);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(InventoryTag)
            .WithName("ListStock")
            .WithSummary("List stock levels for every product")
            .WithDescription(
                "Returns every stock record (on hand, reserved and available), removed products' "
                    + "included, ordered by SKU. Paging defaults to page 1, size 25; the page size is at most 100."
            )
            .Produces<Pagination<StockReadModel>>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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

public sealed record ListStockRequest([FromQuery] int Page = 1, [FromQuery] int PageSize = 25);
