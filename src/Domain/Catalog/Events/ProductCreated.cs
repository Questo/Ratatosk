using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductCreated(Guid productId, string name, string sku, string description, Price price) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public string Name { get; } = name;
    public string Sku { get; } = sku;
    public string Description { get; } = description;
    public Price Price { get; } = price;
}
