using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Ordering.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.Domain.Identity;

namespace Ratatosk.API.Orders;

public static class OrderEndpoints
{
    private const string OrdersTag = "Orders";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/orders",
                async (
                    PlaceOrderRequest request,
                    ClaimsPrincipal user,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    // The customer is the signed-in user; never take it from the request.
                    if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId))
                        return Results.Unauthorized();

                    var command = new PlaceOrderCommand(
                        customerId,
                        [
                            .. (request.Lines ?? []).Select(l => new OrderLineRequest(
                                l.Sku,
                                l.Quantity
                            )),
                        ]
                    );
                    var result = await dispatcher.DispatchAsync(command, ct);
                    var response = Response<Guid>.FromResult(result);

                    return result.IsFailure
                        ? Results.BadRequest(response)
                        : Results.Accepted($"/orders/{result.Value}", response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("PlaceOrder")
            .WithSummary("Place a new order")
            .WithDescription(
                "Creates an order for the signed-in user and reserves stock for each line via async events. "
                    + "The order may already be Confirmed or Cancelled by the time this returns; "
                    + "GET /orders/{orderId} always reflects its current status."
            )
            .Accepts<PlaceOrderRequest>("application/json")
            .Produces<Response>(StatusCodes.Status202Accepted)
            .Produces<Response>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet(
                "/orders/{orderId:guid}",
                async (Guid orderId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var callerId))
                        return Results.Unauthorized();

                    var query = new GetOrderByIdQuery(orderId, callerId, user.IsInRole(UserRole.Admin.Name));
                    var result = await dispatcher.DispatchAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("GetOrderById")
            .WithSummary("Get an order by id")
            .WithDescription(
                "Returns the current read model for the supplied order id. "
                    + "Returns 404 unless the caller placed the order or is an Admin."
            )
            .Produces<OrderReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet(
                "/orders",
                async (
                    [AsParameters] ListMyOrdersRequest request,
                    ClaimsPrincipal user,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    // The customer is the signed-in user; never take it from the request.
                    if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId))
                        return Results.Unauthorized();

                    var query = new GetCustomerOrdersQuery(customerId, request.Page, request.PageSize);
                    var result = await dispatcher.DispatchAsync(query, ct);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("ListMyOrders")
            .WithSummary("List the signed-in customer's orders")
            .WithDescription(
                "Returns the caller's own orders, newest first, with their lines. "
                    + "Paging defaults to page 1, size 25; the page size is at most 100."
            )
            .Produces<Pagination<OrderReadModel>>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record PlaceOrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderRequest(IReadOnlyList<PlaceOrderLineRequest> Lines);

public sealed record ListMyOrdersRequest([FromQuery] int Page = 1, [FromQuery] int PageSize = 25);
