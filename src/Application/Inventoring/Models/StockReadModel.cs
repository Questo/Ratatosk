namespace Ratatosk.Application.Inventoring.Models;

/// <summary>
/// One product's stock, as of <see cref="Version"/> of its Inventory: what is on hand, how much
/// of it is reserved, and the rest, which is available.
/// </summary>
public class StockReadModel
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public int Available => OnHand - Reserved;
    public string Unit { get; set; } = default!;

    /// <summary>The Inventory version this record reflects; a save never replaces a newer one.</summary>
    public int Version { get; set; }
    public DateTime LastUpdatedUtc { get; set; }

    public StockReadModel() { }

    public StockReadModel(
        Guid productId,
        string sku,
        int onHand,
        int reserved,
        string unit,
        int version,
        DateTime lastUpdatedUtc
    )
    {
        ProductId = productId;
        Sku = sku;
        OnHand = onHand;
        Reserved = reserved;
        Unit = unit;
        Version = version;
        LastUpdatedUtc = lastUpdatedUtc;
    }
}
