using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductUpdated(
    Guid productId, string name, Description? description = null,
    Price? price = null) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public string Name { get; } = name;
    public Description Description { get; } = description ?? default!;
    public Price Price { get; } = price ?? default!;
}
