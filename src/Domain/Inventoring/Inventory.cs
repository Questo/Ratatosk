using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Domain.Inventoring;

public class Inventory : AggregateRoot
{
    private readonly Dictionary<SKU, StockLevel> _stockBySku = [];
    private readonly Dictionary<(SKU Sku, Guid OrderId), int> _reservedByOrder = [];

    public DateTimeOffset LastUpdatedUtc { get; private set; }

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        LastUpdatedUtc = domainEvent.OccurredAtUtc;
        StockLevel stock;

        switch (domainEvent)
        {
            case InventoryCreated inventoryCreated:
                Id = inventoryCreated.InventoryId;
                break;

            case StockAdded stockAdded:
                stock = StockOf(stockAdded.SKU);
                _stockBySku[stockAdded.SKU] = stock with
                {
                    Unit = stockAdded.Quantity.Unit,
                    OnHand = stock.OnHand + stockAdded.Quantity.Amount,
                };
                break;

            case StockReserved stockReserved:
                stock = StockOf(stockReserved.SKU);
                _stockBySku[stockReserved.SKU] = stock with { Reserved = stock.Reserved + stockReserved.Quantity };

                if (stockReserved.OrderId is Guid reservedForOrder)
                {
                    var key = (stockReserved.SKU, reservedForOrder);
                    _reservedByOrder[key] =
                        _reservedByOrder.GetValueOrDefault(key) + stockReserved.Quantity;
                }
                break;

            case StockReleased stockReleased:
                stock = StockOf(stockReleased.SKU);
                _stockBySku[stockReleased.SKU] = stock with { Reserved = stock.Reserved - stockReleased.Quantity };

                if (stockReleased.OrderId is Guid releasedForOrder)
                {
                    _reservedByOrder.Remove((stockReleased.SKU, releasedForOrder));
                }
                break;

            case StockRemoved stockRemoved:
                stock = StockOf(stockRemoved.SKU);
                _stockBySku[stockRemoved.SKU] = stock with { OnHand = stock.OnHand - stockRemoved.Quantity.Amount };
                break;
        }
    }

    public static Inventory Create(Guid productId)
    {
        Guard.AgainstEmpty(productId, nameof(productId));

        var inventory = new Inventory();
        inventory.RaiseEvent(new InventoryCreated(productId));
        return inventory;
    }

    /// <summary>The stock held for a SKU; empty (zero pieces) when the SKU has never been stocked.</summary>
    public StockLevel StockOf(SKU sku) =>
        _stockBySku.TryGetValue(sku, out var stock) ? stock : new StockLevel(Quantity.Pieces(0).Unit, 0, 0);

    public bool IsInStock(SKU sku, int quantity) => StockOf(sku).Available >= quantity;

    // Reserved stock not held by any order, i.e. reserved by hand rather than by order placement.
    private int UnallocatedReserved(SKU sku) =>
        StockOf(sku).Reserved
        - _reservedByOrder.Where(r => r.Key.Sku.Equals(sku)).Sum(r => r.Value);

    private static Result EnsureSameUnit(SKU sku, StockLevel stock, Quantity quantity) =>
        string.Equals(stock.Unit, quantity.Unit, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure($"Unit mismatch for SKU {sku}. Expected {stock.Unit}, but got {quantity.Unit}");

    /// <summary>
    /// Adds stock on hand. While nothing is on hand the restock may change the SKU's unit;
    /// after that it must match.
    /// </summary>
    public Result Restock(SKU sku, Quantity quantity)
    {
        if (quantity.Amount <= 0)
            return Result.Failure("Quantity must be greater than zero");

        var stock = StockOf(sku);
        if (stock.OnHand > 0)
        {
            var unit = EnsureSameUnit(sku, stock, quantity);
            if (unit.IsFailure)
                return unit;
        }

        RaiseEvent(new StockAdded(Id, sku, quantity));
        return Result.Success();
    }

    /// <summary>
    /// Reserves available stock, optionally on behalf of an order. A rejected reservation for an
    /// order is recorded as <see cref="StockReservationFailed"/> as well as returned as a failure.
    /// Reserving again for an order that already holds a reservation for this SKU is a no-op.
    /// </summary>
    public Result ReserveStock(SKU sku, Quantity quantity, Guid? orderId = null)
    {
        if (quantity.Amount <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (orderId is Guid heldBy && _reservedByOrder.ContainsKey((sku, heldBy)))
            return Result.Success();

        string? rejection = null;
        if (!_stockBySku.TryGetValue(sku, out var stock))
            rejection = $"SKU {sku} not found in inventory";
        else if (EnsureSameUnit(sku, stock, quantity) is { IsFailure: true } unit)
            rejection = unit.Error;
        else if (stock.Available < quantity.Amount)
            rejection = $"Not enough stock available for SKU {sku}";

        if (rejection is not null)
        {
            if (orderId is Guid rejectedOrderId)
                RaiseEvent(new StockReservationFailed(Id, sku, rejectedOrderId, quantity.Amount, rejection));

            return Result.Failure(rejection);
        }

        RaiseEvent(new StockReserved(Id, sku, quantity.Amount, orderId));
        return Result.Success();
    }

    /// <summary>
    /// Releases stock that was reserved by hand. Stock held by an order can only be released
    /// through <see cref="ReleaseStock(SKU, Guid)"/>.
    /// </summary>
    public Result ReleaseStock(SKU sku, Quantity quantity)
    {
        if (quantity.Amount <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (!_stockBySku.TryGetValue(sku, out var stock))
            return Result.Failure($"SKU {sku} not found in inventory");

        var unit = EnsureSameUnit(sku, stock, quantity);
        if (unit.IsFailure)
            return unit;

        if (UnallocatedReserved(sku) < quantity.Amount)
            return Result.Failure($"Not enough reserved stock for SKU {sku}");

        RaiseEvent(new StockReleased(Id, sku, quantity.Amount));
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
        if (!_stockBySku.TryGetValue(sku, out var stock))
            return Result.Failure($"SKU {sku} not found in inventory");

        var unit = EnsureSameUnit(sku, stock, quantity);
        if (unit.IsFailure)
            return unit;

        if (stock.Available < quantity.Amount)
            return Result.Failure($"Not enough available stock for SKU {sku}");

        RaiseEvent(new StockRemoved(Id, sku, quantity));
        return Result.Success();
    }
}

/// <summary>
/// One SKU's stock, every amount in <see cref="Unit"/>: what is on hand, how much of it is
/// reserved, and the rest, which is available.
/// </summary>
public sealed record StockLevel(string Unit, int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}
