namespace Ratatosk.Domain.Catalog;

public interface IProductDomainService
{
    /// <summary>
    /// Checks if the given SKU is unique within the system.
    /// Returns true if the SKU does not already exist.
    /// </summary>
    Task<bool> IsSkuUniqueAsync(string sku, CancellationToken cancellationToken = default);
}
