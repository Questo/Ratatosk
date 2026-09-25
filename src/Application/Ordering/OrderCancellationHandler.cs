using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering;

public class OrderCancellationHandler(IAggregateRepository<Order> repository, IEventBus eventBus)
    : IDomainEventHandler<StockReservationFailed>
{
    public async Task WhenAsync(
        StockReservationFailed domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var orderResult = await repository.LoadAsync(domainEvent.OrderId, cancellationToken);
        if (orderResult.IsFailure)
            return;

        var order = orderResult.Value!;
        order.Cancel(domainEvent.Reason);

        await repository.SaveAsync(order, cancellationToken);

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
