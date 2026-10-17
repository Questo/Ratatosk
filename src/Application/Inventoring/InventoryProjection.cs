using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Application.Inventoring;

/// <summary>
/// The stock read model's only writer. Every stock event rewrites the record from the
/// (committed) Inventory rather than applying the event as a delta, and the record carries the
/// Inventory version it reflects. A late, repeated or concurrently handled event therefore can't
/// leave the record behind the Inventory.
/// </summary>
// No ProductRemoved handler: the stock record outlives its product, because orders may still
// hold reservations against it that must be released.
public class InventoryProjection(IInventoryReadModelRepository repo, IAggregateRepository<Inventory> inventories)
    : IDomainEventHandler<ProductCreated>,
        IDomainEventHandler<StockAdded>,
        IDomainEventHandler<StockReserved>,
        IDomainEventHandler<StockReleased>,
        IDomainEventHandler<StockRemoved>
{
    // Version 0: an empty record never replaces one written from the Inventory.
    public Task WhenAsync(ProductCreated domainEvent, CancellationToken cancellationToken) =>
        repo.SaveAsync(
            new StockReadModel(
                domainEvent.ProductId,
                domainEvent.Sku.Value,
                onHand: 0,
                reserved: 0,
                Quantity.Pieces(0).Unit,
                version: 0,
                domainEvent.OccurredAtUtc.UtcDateTime
            ),
            cancellationToken
        );

    public Task WhenAsync(StockAdded domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.InventoryId, domainEvent.SKU, cancellationToken);

    public Task WhenAsync(StockReserved domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.InventoryId, domainEvent.SKU, cancellationToken);

    public Task WhenAsync(StockReleased domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.InventoryId, domainEvent.SKU, cancellationToken);

    public Task WhenAsync(StockRemoved domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.InventoryId, domainEvent.SKU, cancellationToken);

    // An Inventory's id is its product's id.
    private async Task SyncAsync(Guid inventoryId, SKU sku, CancellationToken cancellationToken)
    {
        var inventoryResult = await inventories.LoadAsync(inventoryId, cancellationToken);
        if (inventoryResult.IsFailure)
            throw new InvalidOperationException(inventoryResult.Error);

        var inventory = inventoryResult.Value!;
        var stock = inventory.StockOf(sku);

        await repo.SaveAsync(
            new StockReadModel(
                inventory.Id,
                sku.Value,
                stock.OnHand,
                stock.Reserved,
                stock.Unit,
                inventory.Version,
                inventory.LastUpdatedUtc.UtcDateTime
            ),
            cancellationToken
        );
    }
}
