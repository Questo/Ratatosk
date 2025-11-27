using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Infrastructure.Services;

public class InventoryDomainService(IAggregateRepository<Inventory> repository)
    : IInventoryDomainService,
        IDomainEventHandler<ProductCreated>,
        IDomainEventHandler<ProductRemoved>
{
    public Task<bool> IsProductInStockAsync(
        Guid productId,
        int? quantity = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsProductInStockAsync(
        string sku,
        int? quantity = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> ReserveProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> ReserveProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> RestockProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> RestockProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> UnreserveProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Result> UnreserveProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public async Task WhenAsync(
        ProductCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var inventory = await repository.LoadAsync(domainEvent.ProductId, cancellationToken);
        if (inventory.IsFailure)
            return;

        throw new NotImplementedException();
    }

    public Task WhenAsync(ProductRemoved domainEvent, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
