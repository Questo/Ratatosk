using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderCancelled(Guid orderId, string reason, IReadOnlyList<OrderLine> lines)
    : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string Reason { get; } = reason;
    public IReadOnlyList<OrderLine> Lines { get; } = lines;
}
