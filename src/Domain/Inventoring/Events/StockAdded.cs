using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockAdded(Guid inventoryId, SKU sku, int quantity) : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public int Quantity { get; } = quantity;
}
