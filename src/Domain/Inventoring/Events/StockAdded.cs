using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockAdded(Guid inventoryId, SKU sku, Quantity quantity) : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public Quantity Quantity { get; } = quantity;
}
