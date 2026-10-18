using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Ordering;

/// <summary>
/// The Order read model's only writer. Every Order event rewrites the row from the (committed)
/// Order aggregate rather than applying the event as a delta. Events are delivered depth-first,
/// so OrderConfirmed can be handled before OrderCreated is; reading the aggregate makes the
/// outcome independent of that order.
/// </summary>
public class OrderProjection(IOrderReadModelRepository repo, IAggregateRepository<Order> orders)
    : IDomainEventHandler<OrderCreated>,
        IDomainEventHandler<OrderConfirmed>,
        IDomainEventHandler<OrderCancelled>
{
    public Task WhenAsync(OrderCreated domainEvent, CancellationToken cancellationToken = default) =>
        SyncAsync(domainEvent.OrderId, cancellationToken);

    public Task WhenAsync(OrderConfirmed domainEvent, CancellationToken cancellationToken = default) =>
        SyncAsync(domainEvent.OrderId, cancellationToken);

    public Task WhenAsync(OrderCancelled domainEvent, CancellationToken cancellationToken = default) =>
        SyncAsync(domainEvent.OrderId, cancellationToken);

    private async Task SyncAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var orderResult = await orders.LoadAsync(orderId, cancellationToken);
        if (orderResult.IsFailure)
            throw new InvalidOperationException(orderResult.Error);

        await repo.SaveAsync(ToReadModel(orderResult.Value!), cancellationToken);
    }

    private static OrderReadModel ToReadModel(Order order) =>
        new()
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            Lines =
            [
                .. order.Lines.Select(l => new OrderLineReadModel(
                    l.Sku.Value,
                    l.Quantity,
                    l.UnitPrice.Amount,
                    l.UnitPrice.Currency
                )),
            ],
            CreatedUtc = order.PlacedAtUtc.UtcDateTime,
            LastUpdatedUtc = order.LastUpdatedUtc.UtcDateTime,
            CancellationReason = order.CancellationReason,
        };
}
