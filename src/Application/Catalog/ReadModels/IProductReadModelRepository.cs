namespace Ratatosk.Application.Catalog.ReadModels;

public interface IProductReadModelRepository
{
    Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default);
}