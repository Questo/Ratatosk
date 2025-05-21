namespace Ratatosk.Application.Catalog.ReadModels;

public interface IProductReadModelRepository
{
    Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductReadModel>> GetAllAsync(string? searchTerm = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
