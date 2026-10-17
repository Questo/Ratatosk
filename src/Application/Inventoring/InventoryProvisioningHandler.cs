using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring;

public class InventoryProvisioningHandler(IAggregateRepository<Inventory> repository)
    : IDomainEventHandler<ProductCreated>
{
    public async Task WhenAsync(
        ProductCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var inventory = Inventory.Create(domainEvent.ProductId);

        await repository.CommitAsync(inventory, cancellationToken);
    }
}
