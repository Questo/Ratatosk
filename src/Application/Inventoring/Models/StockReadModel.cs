namespace Ratatosk.Application.Inventoring.Models;

public class StockReadModel
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public int Available { get; set; }
    public int Reserved { get; set; }
    public string Unit { get; set; } = default!;
    public DateTime LastUpdatedUtc { get; set; }

    public StockReadModel() { }

    public StockReadModel(
        Guid productId,
        string sku,
        int available,
        int reserved,
        string unit,
        DateTime lastUpdatedUtc
    )
    {
        ProductId = productId;
        Sku = sku;
        Available = available;
        Reserved = reserved;
        Unit = unit;
        LastUpdatedUtc = lastUpdatedUtc;
    }
}
