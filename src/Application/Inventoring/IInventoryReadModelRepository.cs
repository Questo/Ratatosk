using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;

namespace Ratatosk.Application.Inventoring;

public interface IInventoryReadModelRepository
{
    Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    );
    Task<StockReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Stock record, ordered by SKU and then product id. With <paramref name="maxAvailable"/>,
    /// only records with at most that many available, ordered by available, then SKU, then product id.
    /// </summary>
    Task<Pagination<StockReadModel>> GetPageAsync(
        int page = 1,
        int pageSize = 25,
        int? maxAvailable = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Inserts or replaces the product's record, unless the stored one reflects the same or a
    /// newer Inventory version (concurrent projections may finish out of order).
    /// </summary>
    Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default);
}
