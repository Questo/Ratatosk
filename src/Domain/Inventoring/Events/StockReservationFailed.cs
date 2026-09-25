using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class StockReservationFailed(
    Guid inventoryId,
    SKU sku,
    Guid orderId,
    int quantity,
    string reason
) : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
    public SKU SKU { get; } = sku;
    public Guid OrderId { get; } = orderId;
    public int Quantity { get; } = quantity;
    public string Reason { get; } = reason;
}
