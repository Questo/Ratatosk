using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductAdded(Guid productId, string name, string sku, Price price) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public string Name { get; } = name;
    public string Sku { get; } = sku;
    public Price Price { get; } = price;
}
