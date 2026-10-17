using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring;

/// <summary>
/// Every change to stock goes through here: finding the Inventory that holds a product's or a
/// SKU's stock, applying the change, and committing it, retrying when a concurrent writer
/// committed to the same Inventory first.
/// </summary>
public sealed class StockKeeper(
    IAggregateRepository<Inventory> inventories,
    IInventoryReadModelRepository stockRecords
)
{
    private const int MaxAttempts = 3;

    public async Task<Result> RestockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await stockRecords.GetByProductIdAsync(productId, cancellationToken);
        if (stock is null)
            return Result.Failure($"No stock record found for product {productId}");

        var quantityResult = Quantity.Create(quantity, stock.Unit);
        if (quantityResult.IsFailure)
            return Result.Failure(quantityResult.Error!);

        return await ChangeAsync(
            stock,
            (inventory, sku) => inventory.AddStock(sku, quantityResult.Value!),
            cancellationToken
        );
    }

    /// <summary>Reserves stock by hand, outside of any order.</summary>
    public async Task<Result> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await stockRecords.GetByProductIdAsync(productId, cancellationToken);
        if (stock is null)
            return Result.Failure($"No stock record found for product {productId}");

        return await ChangeAsync(
            stock,
            (inventory, sku) => inventory.ReserveStock(sku, quantity),
            cancellationToken
        );
    }

    /// <summary>Releases stock reserved by hand. Stock held by an order is left alone.</summary>
    public async Task<Result> ReleaseAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await stockRecords.GetByProductIdAsync(productId, cancellationToken);
        if (stock is null)
            return Result.Failure($"No stock record found for product {productId}");

        return await ChangeAsync(
            stock,
            (inventory, sku) => inventory.ReleaseStock(sku, quantity),
            cancellationToken
        );
    }

    /// <summary>
    /// Reserves stock for one Order line. Idempotent per order and SKU. A failure carries the
    /// reason the reservation was rejected; the rejection is also recorded on the Inventory.
    /// </summary>
    public async Task<Result> ReserveForOrderAsync(
        SKU sku,
        int quantity,
        Guid orderId,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await stockRecords.GetBySkuAsync(sku.Value, cancellationToken);
        if (stock is null)
            return Result.Failure($"SKU {sku} has no inventory record");

        return await ChangeAsync(
            stock,
            (inventory, _) => inventory.ReserveStock(sku, quantity, orderId),
            cancellationToken
        );
    }

    /// <summary>
    /// Releases whatever the order holds for this SKU; a no-op if it holds nothing.
    /// </summary>
    public async Task ReleaseForOrderAsync(
        SKU sku,
        Guid orderId,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await stockRecords.GetBySkuAsync(sku.Value, cancellationToken);
        if (stock is null)
            return; // No inventory record, so the order cannot have reserved anything here.

        var result = await ChangeAsync(
            stock,
            (inventory, _) =>
            {
                inventory.ReleaseStock(sku, orderId);
                return Result.Success();
            },
            cancellationToken
        );

        // Silently skipping would leak the reservation, so make the failure loud.
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Could not release stock for SKU {sku} held by order {orderId}: {result.Error}"
            );
    }

    // The change's own Result is returned, but its events are committed either way: a rejected
    // reservation for an order is recorded as StockReservationFailed.
    private async Task<Result> ChangeAsync(
        StockReadModel stock,
        Func<Inventory, SKU, Result> change,
        CancellationToken cancellationToken
    )
    {
        var skuResult = SKU.Create(stock.Sku);
        if (skuResult.IsFailure)
            return Result.Failure(skuResult.Error!);

        for (var attempt = 1; ; attempt++)
        {
            var inventoryResult = await inventories.LoadAsync(stock.ProductId, cancellationToken);
            if (inventoryResult.IsFailure)
                return Result.Failure(inventoryResult.Error!);

            var inventory = inventoryResult.Value!;
            var outcome = change(inventory, skuResult.Value!);

            try
            {
                await inventories.CommitAsync(inventory, cancellationToken);
                return outcome;
            }
            catch (ConcurrencyException) when (attempt < MaxAttempts)
            {
                // Another writer got there first: reload and decide again on fresh stock levels.
            }
        }
    }
}
