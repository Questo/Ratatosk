using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering;

public class OrderCancellationHandler(
    IAggregateRepository<Order> repository,
    IEventBus eventBus,
    IUnitOfWork uow
) : IDomainEventHandler<StockReservationFailed>
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

        // Commit before publishing: OrderProjection and ReservationCompensationHandler (nested
        // via the OrderCancelled publish below) read this Order back from other connections.
        uow.Commit();

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
