using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductUpdated(
    Guid productId, string name, string? description = null,
    decimal? price = null) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public string Name { get; } = name;
    public string Description { get; } = description ?? default!;
    public decimal Price { get; } = price ?? default!;
}
