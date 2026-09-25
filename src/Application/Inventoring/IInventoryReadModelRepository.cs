using Ratatosk.Application.Inventoring.Models;

namespace Ratatosk.Application.Inventoring;

public interface IInventoryReadModelRepository
{
    Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    );
    Task<StockReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default);
}
