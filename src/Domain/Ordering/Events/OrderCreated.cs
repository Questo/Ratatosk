using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering.Events;

public sealed class OrderCreated(Guid orderId, Guid customerId, IReadOnlyList<OrderLine> lines)
    : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid CustomerId { get; } = customerId;
    public IReadOnlyList<OrderLine> Lines { get; } = lines;
}
