using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderConfirmed(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}
