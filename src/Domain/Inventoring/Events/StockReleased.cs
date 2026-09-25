using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockReleased(Guid inventoryId, SKU sku, int quantity, Guid? orderId = null)
    : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public int Quantity { get; } = quantity;
    public Guid? OrderId { get; } = orderId;
}
