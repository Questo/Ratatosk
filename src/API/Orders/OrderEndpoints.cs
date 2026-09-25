using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Ordering.Queries;

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
                    IOrderService orderService,
                    CancellationToken ct
                ) =>
                {
                    var command = new PlaceOrderCommand(
                        request.CustomerId,
                        [.. request.Lines.Select(l => new OrderLineRequest(l.Sku, l.Quantity))]
                    );
                    var result = await orderService.PlaceOrderAsync(command, ct);
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
                "Creates an order and asynchronously reserves stock for each line. "
                    + "Poll GET /orders/{orderId} to observe confirmation or cancellation."
            )
            .Accepts<PlaceOrderRequest>("application/json")
            .Produces<Response>(StatusCodes.Status202Accepted)
            .Produces<Response>(StatusCodes.Status400BadRequest);

        app.MapGet(
                "/orders/{orderId:guid}",
                async (Guid orderId, IOrderService orderService, CancellationToken ct) =>
                {
                    var query = new GetOrderByIdQuery(orderId);
                    var result = await orderService.GetOrderByIdAsync(query, ct);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                }
            )
            .RequireAuthorization(Policies.Authenticated)
            .WithTags(OrdersTag)
            .WithName("GetOrderById")
            .WithSummary("Get an order by id")
            .WithDescription("Returns the current read model for the supplied order id.")
            .Produces<OrderReadModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record PlaceOrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderRequest(Guid CustomerId, IReadOnlyList<PlaceOrderLineRequest> Lines);
