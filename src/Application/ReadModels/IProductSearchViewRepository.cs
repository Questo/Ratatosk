namespace Ratatosk.Application.ReadModels;

public interface IProductSearchViewRepository
{
    Task<ProductSearchView> GetViewAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ProductSearchView view, CancellationToken cancellationToken = default);
}