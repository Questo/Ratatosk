using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Ordering;

public class OrderProjection(IOrderReadModelRepository repo)
    : IDomainEventHandler<OrderCreated>,
        IDomainEventHandler<OrderConfirmed>,
        IDomainEventHandler<OrderCancelled>
{
    public async Task WhenAsync(
        OrderCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = new OrderReadModel
        {
            Id = domainEvent.OrderId,
            CustomerId = domainEvent.CustomerId,
            Status = nameof(OrderStatus.Created),
            Lines =
            [
                .. domainEvent.Lines.Select(l => new OrderLineReadModel(
                    l.Sku.Value,
                    l.Quantity,
                    l.UnitPrice.Amount,
                    l.UnitPrice.Currency
                )),
            ],
            CreatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(
        OrderConfirmed domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await repo.GetByIdAsync(domainEvent.OrderId, cancellationToken);
        if (existing is null)
            return;

        existing.Status = nameof(OrderStatus.Confirmed);
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(
        OrderCancelled domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await repo.GetByIdAsync(domainEvent.OrderId, cancellationToken);
        if (existing is null)
            return;

        existing.Status = nameof(OrderStatus.Cancelled);
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }
}
