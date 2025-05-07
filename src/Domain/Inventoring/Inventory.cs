using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Domain.Inventoring;

public class Inventory : AggregateRoot
{
    private readonly Dictionary<SKU, StockEntry> _stockBySku = [];

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        StockEntry? entry;

        switch (domainEvent)
        {
            case StockAdded stockAdded:
                entry = GetStockEntry(stockAdded.SKU);
                entry = entry with { Available = entry.Available + stockAdded.Quantity };
                _stockBySku[stockAdded.SKU] = entry;
                break;

            case StockReserved stockReserved:
                entry = GetStockEntry(stockReserved.SKU);
                entry = entry with { Reserved = entry.Reserved + stockReserved.Quantity };
                _stockBySku[stockReserved.SKU] = entry;
                break;

            case StockReleased stockReleased:
                entry = GetStockEntry(stockReleased.SKU);
                entry = entry with { Reserved = entry.Reserved - stockReleased.Quantity };
                _stockBySku[stockReleased.SKU] = entry;
                break;
        }
    }

    public static Inventory Create()
    {
        var inventory = new Inventory();
        inventory.RaiseEvent(new InventoryCreated(inventory.Id));
        return inventory;
    }

    private StockEntry GetStockEntry(SKU sku)
    {
        return _stockBySku.TryGetValue(sku, out var stockEntry)
            ? stockEntry
            : new StockEntry(Quantity.Pieces(0), 0);
    }

    public void AddStock(SKU sku, Quantity quantity)
    {
        RaiseEvent(new StockAdded(Id, sku, quantity));
    }

    public void ReserveStock(SKU sku, int quantity)
    {
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        if (!_stockBySku.TryGetValue(sku, out var stockEntry))
        {
            throw new InvalidOperationException($"SKU {sku} not found in inventory");
        }

        if (stockEntry.Available.Amount - stockEntry.Reserved < quantity)
        {
            throw new InvalidOperationException($"Not enough stock available for SKU {sku}");
        }

        RaiseEvent(new StockReserved(Id, sku, quantity));
    }

    public void ReleaseStock(SKU sku, int quantity)
    {
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        if (!_stockBySku.TryGetValue(sku, out var stockEntry))
        {
            throw new InvalidOperationException($"SKU {sku} not found in inventory");
        }

        if (stockEntry.Reserved < quantity)
        {
            throw new InvalidOperationException($"Not enough reserved stock for SKU {sku}");
        }

        RaiseEvent(new StockReleased(Id, sku, quantity));
    }
}

public sealed record StockEntry(Quantity Available, int Reserved);