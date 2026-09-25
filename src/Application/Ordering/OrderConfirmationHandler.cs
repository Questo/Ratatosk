using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering;

public class OrderConfirmationHandler(
    IAggregateRepository<Order> repository,
    IEventBus eventBus,
    IUnitOfWork uow
) : IDomainEventHandler<StockReserved>
{
    public async Task WhenAsync(
        StockReserved domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        if (domainEvent.OrderId is not Guid orderId)
            return;

        var orderResult = await repository.LoadAsync(orderId, cancellationToken);
        if (orderResult.IsFailure)
            return;

        var order = orderResult.Value!;
        order.MarkLineReserved(domainEvent.SKU);

        await repository.SaveAsync(order, cancellationToken);

        // Commit before publishing so nested handlers can read this Order back.
        uow.Commit();

        foreach (var raised in order.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
