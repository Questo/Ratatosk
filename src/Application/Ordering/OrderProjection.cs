using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Ordering;

// The initial row is created by PlaceOrderCommandHandler, not here — see its comment for why.
public class OrderProjection(IOrderReadModelRepository repo, IAggregateRepository<Order> orderRepository)
    : IDomainEventHandler<OrderConfirmed>,
        IDomainEventHandler<OrderCancelled>
{
    public async Task WhenAsync(
        OrderConfirmed domainEvent,
        CancellationToken cancellationToken = default
    ) => await SyncFromAggregateAsync(domainEvent.OrderId, domainEvent.OccurredAtUtc.UtcDateTime, cancellationToken);

    public async Task WhenAsync(
        OrderCancelled domainEvent,
        CancellationToken cancellationToken = default
    ) => await SyncFromAggregateAsync(domainEvent.OrderId, domainEvent.OccurredAtUtc.UtcDateTime, cancellationToken);

    // Reads the Order aggregate (always committed by now) rather than trusting Status is fresh.
    private async Task SyncFromAggregateAsync(
        Guid orderId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken
    )
    {
        var orderResult = await orderRepository.LoadAsync(orderId, cancellationToken);
        if (orderResult.IsFailure)
            return;

        var order = orderResult.Value!;
        var existing = await repo.GetByIdAsync(orderId, cancellationToken);

        var readModel = new OrderReadModel
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
            CreatedUtc = existing?.CreatedUtc ?? occurredAtUtc,
            LastUpdatedUtc = occurredAtUtc,
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }
}
