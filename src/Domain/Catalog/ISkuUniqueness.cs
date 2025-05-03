namespace Ratatosk.Domain.Catalog;

public interface ISkuUniqueness
{
    /// <summary>
    /// Checks if the given SKU is unique within the system.
    /// Returns true if the SKU does not already exist.
    /// </summary>
    Task<bool> IsUniqueAsync(string sku, CancellationToken cancellationToken = default);
}