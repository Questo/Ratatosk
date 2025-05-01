using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductRemoved(Guid productId, string? reason) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public string? Reason { get; } = reason;
}