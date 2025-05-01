using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Catalog;

public class ProductSnapshot : Snapshot
{
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Price Price { get; set; } = default!;
}