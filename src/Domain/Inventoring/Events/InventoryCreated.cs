using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Inventoring.Events;

public sealed class InventoryCreated(Guid inventoryId) : DomainEvent
{
    public Guid InventoryId { get; } = inventoryId;
}
