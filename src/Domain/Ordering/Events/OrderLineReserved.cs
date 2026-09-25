using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderLineReserved(Guid orderId, SKU sku) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public SKU Sku { get; } = sku;
}
