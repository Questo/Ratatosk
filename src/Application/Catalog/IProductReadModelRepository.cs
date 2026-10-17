using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Shared;

namespace Ratatosk.Application.Catalog;

public interface IProductReadModelRepository
{
    Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pagination<ProductReadModel>> GetAllAsync(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 25,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        CancellationToken cancellationToken = default
    );
    Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
