using System.Text.Json.Serialization;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog.Events;

public sealed class ProductCreated(Guid productId, ProductName name, SKU sku, Description description, Price price) : DomainEvent
{
    public Guid ProductId { get; } = productId;
    public ProductName Name { get; } = name;
    public SKU Sku { get; } = sku;
    public Description Description { get; } = description;
    public Price Price { get; } = price;
}
