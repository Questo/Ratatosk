namespace Ratatosk.Application.Catalog.ReadModels;

public class ProductReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}