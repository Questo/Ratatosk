using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Ordering;

public sealed class OrderRenamed(Guid orderId, string oldName, string newName) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string OldName { get; set; } = oldName;
    public string NewName { get; } = newName;
}