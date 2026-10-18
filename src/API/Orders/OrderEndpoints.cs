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
                    var response = Response<OrderReadModel>.FromResult(result);

                    return result.IsFailure ? Results.NotFound(response) : Results.Ok(response);
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
            .Produces<Response<OrderReadModel>>(StatusCodes.Status200OK)
            .Produces<Response<OrderReadModel>>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet(
                "/orders",
                async (
                    [AsParameters] ListOrdersRequest request,
                    ClaimsPrincipal user,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    if (user.IsInRole(UserRole.Admin.Name))
                    {
                        var adminQuery = new GetOrdersQuery(
                            request.CustomerId,
                            request.Status,
                            request.Page,
                            request.PageSize
                        );
                        var adminResult = await dispatcher.DispatchAsync(adminQuery, ct);
                        var adminResponse = Response<Pagination<OrderReadModel>>.FromResult(adminResult);

                        return adminResult.IsFailure
                            ? Results.BadRequest(adminResponse)
                            : Results.Ok(adminResponse);
                    }

                    // The customer is the signed-in user; never take it from the request.
                    if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId))
                        return Results.Unauthorized();

                    var query = new GetCustomerOrdersQuery(
                        customerId,
                        request.Page,
                        request.PageSize,
                        request.Status
                    );
                    var result = await dispatcher.DispatchAsync(query, ct);
                    var response = Response<Pagination<OrderReadModel>>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("ListOrders")
            .WithSummary("List orders: every order for an Admin, otherwise the caller's own")
            .WithDescription(
                "Returns orders newest first, with their lines. An Admin sees every customer's orders "
                    + "and may narrow them with customerId; anyone else sees only their own orders, "
                    + "and customerId is ignored. The optional status filter (an order status name, "
                    + "any case) applies to both. Paging defaults to page 1, size 25; the page size is at most 100."
            )
            .Produces<Response<Pagination<OrderReadModel>>>(StatusCodes.Status200OK)
            .Produces<Response<Pagination<OrderReadModel>>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record PlaceOrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderRequest(IReadOnlyList<PlaceOrderLineRequest> Lines);

public sealed record ListOrdersRequest(
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 25,
    [FromQuery] string? Status = null,
    [FromQuery] Guid? CustomerId = null
);
