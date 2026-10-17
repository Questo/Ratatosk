using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Domain.Inventoring;

public class Inventory : AggregateRoot
{
    private readonly Dictionary<SKU, StockEntry> _stockBySku = [];
    private readonly Dictionary<(SKU Sku, Guid OrderId), int> _reservedByOrder = [];

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        StockEntry? entry;

        switch (domainEvent)
        {
            case InventoryCreated inventoryCreated:
                Id = inventoryCreated.InventoryId;
                break;

            case StockAdded stockAdded:
                entry = GetStockEntry(stockAdded.SKU);
                entry = entry.Available.Amount == 0
                    ? entry with { Available = stockAdded.Quantity }
                    : entry with { Available = entry.Available + stockAdded.Quantity };
                _stockBySku[stockAdded.SKU] = entry;
                break;

            case StockReserved stockReserved:
                entry = GetStockEntry(stockReserved.SKU);
                entry = entry with { Reserved = entry.Reserved + stockReserved.Quantity };
                _stockBySku[stockReserved.SKU] = entry;

                if (stockReserved.OrderId is Guid reservedForOrder)
                {
                    var key = (stockReserved.SKU, reservedForOrder);
                    _reservedByOrder[key] =
                        _reservedByOrder.GetValueOrDefault(key) + stockReserved.Quantity;
                }
                break;

            case StockReleased stockReleased:
                entry = GetStockEntry(stockReleased.SKU);
                entry = entry with { Reserved = entry.Reserved - stockReleased.Quantity };
                _stockBySku[stockReleased.SKU] = entry;

                if (stockReleased.OrderId is Guid releasedForOrder)
                {
                    _reservedByOrder.Remove((stockReleased.SKU, releasedForOrder));
                }
                break;

            case StockRemoved stockRemoved:
                entry = GetStockEntry(stockRemoved.SKU);
                entry = entry with { Available = entry.Available - stockRemoved.Quantity };
                _stockBySku[stockRemoved.SKU] = entry;
                break;
        }
    }

    public static Inventory Create()
    {
        var inventory = new Inventory();
        inventory.RaiseEvent(new InventoryCreated(inventory.Id));
        return inventory;
    }

    public static Inventory Create(Guid productId)
    {
        Guard.AgainstEmpty(productId, nameof(productId));

        var inventory = new Inventory();
        inventory.RaiseEvent(new InventoryCreated(productId));
        return inventory;
    }

    public bool IsInStock(SKU sku, int quantity)
    {
        var entry = GetStockEntry(sku);
        return entry.Available.Amount - entry.Reserved >= quantity;
    }

    private StockEntry GetStockEntry(SKU sku)
    {
        return _stockBySku.TryGetValue(sku, out var stockEntry)
            ? stockEntry
            : new StockEntry(Quantity.Pieces(0), 0);
    }

    // Reserved stock not held by any order, i.e. reserved by hand rather than by order placement.
    private int UnallocatedReserved(SKU sku) =>
        GetStockEntry(sku).Reserved
        - _reservedByOrder.Where(r => r.Key.Sku.Equals(sku)).Sum(r => r.Value);

    public Result AddStock(SKU sku, Quantity quantity)
    {
        if (quantity.Amount <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (
            _stockBySku.TryGetValue(sku, out var stockEntry)
            && stockEntry.Available.Amount > 0
            && !string.Equals(stockEntry.Available.Unit, quantity.Unit, StringComparison.OrdinalIgnoreCase)
        )
        {
            return Result.Failure(
                $"Unit mismatch for SKU {sku}. Expected {stockEntry.Available.Unit}, but got {quantity.Unit}"
            );
        }

        RaiseEvent(new StockAdded(Id, sku, quantity));
        return Result.Success();
    }

    /// <summary>
    /// Reserves stock, optionally on behalf of an order. A rejected reservation for an order is
    /// recorded as <see cref="StockReservationFailed"/> as well as returned as a failure.
    /// Reserving again for an order that already holds a reservation for this SKU is a no-op.
    /// </summary>
    public Result ReserveStock(SKU sku, int quantity, Guid? orderId = null)
    {
        if (quantity <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (orderId is Guid heldBy && _reservedByOrder.ContainsKey((sku, heldBy)))
            return Result.Success();

        string? rejection = null;
        if (!_stockBySku.ContainsKey(sku))
            rejection = $"SKU {sku} not found in inventory";
        else if (!IsInStock(sku, quantity))
            rejection = $"Not enough stock available for SKU {sku}";

        if (rejection is not null)
        {
            if (orderId is Guid rejectedOrderId)
                RaiseEvent(new StockReservationFailed(Id, sku, rejectedOrderId, quantity, rejection));

            return Result.Failure(rejection);
        }

        RaiseEvent(new StockReserved(Id, sku, quantity, orderId));
        return Result.Success();
    }

    /// <summary>
    /// Releases stock that was reserved by hand. Stock held by an order can only be released
    /// through <see cref="ReleaseStock(SKU, Guid)"/>.
    /// </summary>
    public Result ReleaseStock(SKU sku, int quantity)
    {
        if (quantity <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (!_stockBySku.ContainsKey(sku))
            return Result.Failure($"SKU {sku} not found in inventory");

        if (UnallocatedReserved(sku) < quantity)
            return Result.Failure($"Not enough reserved stock for SKU {sku}");

        RaiseEvent(new StockReleased(Id, sku, quantity));
        return Result.Success();
    }

    // Releases exactly what the order reserved for this SKU; a safe no-op if it reserved nothing.
    public void ReleaseStock(SKU sku, Guid orderId)
    {
        if (!_reservedByOrder.TryGetValue((sku, orderId), out var reservedQuantity))
            return;

        RaiseEvent(new StockReleased(Id, sku, reservedQuantity, orderId));
    }

    public Result RemoveStock(SKU sku, Quantity quantity)
    {
        if (!_stockBySku.TryGetValue(sku, out var stockEntry))
            return Result.Failure($"SKU {sku} not found in inventory");

        if (!string.Equals(stockEntry.Available.Unit, quantity.Unit, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(
                $"Unit mismatch for SKU {sku}. Expected {stockEntry.Available.Unit}, but got {quantity.Unit}"
            );

        if (!IsInStock(sku, quantity.Amount))
            return Result.Failure($"Not enough available stock for SKU {sku}");

        RaiseEvent(new StockRemoved(Id, sku, quantity));
        return Result.Success();
    }
}

public sealed record StockEntry(Quantity Available, int Reserved);
