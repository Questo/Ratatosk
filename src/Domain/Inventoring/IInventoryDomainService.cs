using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Inventoring;

public interface IInventoryDomainService
{
    Task<bool> IsProductInStockAsync(Guid productId, int? quantity = null, CancellationToken cancellationToken = default);
    Task<bool> IsProductInStockAsync(string sku, int? quantity = null, CancellationToken cancellationToken = default);

    Task<Result> ReserveProductAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task<Result> ReserveProductAsync(string sku, int quantity, CancellationToken cancellationToken = default);

    Task<Result> UnreserveProductAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task<Result> UnreserveProductAsync(string sku, int quantity, CancellationToken cancellationToken = default);

    Task<Result> RestockProductAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task<Result> RestockProductAsync(string sku, int quantity, CancellationToken cancellationToken = default);
}
