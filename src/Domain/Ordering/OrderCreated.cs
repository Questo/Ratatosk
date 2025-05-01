using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering;

public sealed class OrderCreated(Guid orderId, string name) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string Name { get; } = name;
}
