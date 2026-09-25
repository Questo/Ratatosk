using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Application.Inventoring;

public class InventoryProjection(IInventoryReadModelRepository repo)
    : IDomainEventHandler<ProductCreated>,
        IDomainEventHandler<ProductRemoved>,
        IDomainEventHandler<StockAdded>,
        IDomainEventHandler<StockReserved>,
        IDomainEventHandler<StockReleased>,
        IDomainEventHandler<StockRemoved>
{
    public async Task WhenAsync(ProductCreated domainEvent, CancellationToken cancellationToken)
    {
        var readModel = new StockReadModel
        {
            ProductId = domainEvent.ProductId,
            Sku = domainEvent.Sku.Value,
            Available = 0,
            Reserved = 0,
            Unit = "pcs",
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(
        ProductRemoved domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await repo.DeleteAsync(domainEvent.ProductId, cancellationToken);
    }

    public async Task WhenAsync(StockAdded domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByProductIdAsync(domainEvent.InventoryId, cancellationToken);
        if (existing is null)
            return;

        existing.Available += domainEvent.Quantity.Amount;
        existing.Unit = domainEvent.Quantity.Unit;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(StockReserved domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByProductIdAsync(domainEvent.InventoryId, cancellationToken);
        if (existing is null)
            return;

        existing.Reserved += domainEvent.Quantity;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(StockReleased domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByProductIdAsync(domainEvent.InventoryId, cancellationToken);
        if (existing is null)
            return;

        existing.Reserved -= domainEvent.Quantity;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(StockRemoved domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByProductIdAsync(domainEvent.InventoryId, cancellationToken);
        if (existing is null)
            return;

        existing.Available -= domainEvent.Quantity.Amount;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }
}
