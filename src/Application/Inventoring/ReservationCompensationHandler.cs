using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Inventoring;

public class ReservationCompensationHandler(
    IInventoryReadModelRepository readModelRepository,
    IAggregateRepository<Inventory> repository,
    IEventBus eventBus,
    IUnitOfWork uow
) : IDomainEventHandler<OrderCancelled>
{
    public async Task WhenAsync(
        OrderCancelled domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var line in domainEvent.Lines)
        {
            var readModel = await readModelRepository.GetBySkuAsync(
                line.Sku.Value,
                cancellationToken
            );
            if (readModel is null)
                continue;

            var inventoryResult = await repository.LoadAsync(
                readModel.ProductId,
                cancellationToken
            );
            if (inventoryResult.IsFailure)
                continue;

            var inventory = inventoryResult.Value!;

            try
            {
                inventory.ReleaseStock(line.Sku, line.Quantity);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            await repository.SaveAsync(inventory, cancellationToken);
            uow.Commit();

            foreach (var raised in inventory.UncommittedEvents)
                await eventBus.PublishAsync(raised, cancellationToken);
        }
    }
}
