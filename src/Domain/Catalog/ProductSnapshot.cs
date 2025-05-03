using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog;

public class ProductSnapshot : Snapshot
{
    public ProductName Name { get; set; } = default!;
    public SKU Sku { get; set; } = default!;
    public Description Description { get; set; } = default!;
    public Price Price { get; set; } = default!;
}