using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring;

public class InventoryProvisioningHandler(
    IAggregateRepository<Inventory> repository,
    IEventBus eventBus
) : IDomainEventHandler<ProductCreated>
{
    public async Task WhenAsync(
        ProductCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var inventory = Inventory.Create(domainEvent.ProductId);

        await repository.SaveAsync(inventory, cancellationToken);

        foreach (var raised in inventory.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);
    }
}
