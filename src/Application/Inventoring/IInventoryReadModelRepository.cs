using Ratatosk.Application.Inventoring.Models;

namespace Ratatosk.Application.Inventoring;

public interface IInventoryReadModelRepository
{
    Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    );
    Task<StockReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or replaces the product's record, unless the stored one reflects the same or a
    /// newer Inventory version (concurrent projections may finish out of order).
    /// </summary>
    Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default);
}
