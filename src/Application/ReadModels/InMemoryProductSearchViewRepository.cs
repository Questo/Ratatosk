namespace Ratatosk.Application.ReadModels;

public class InMemoryProductSearchViewRepository : IProductSearchViewRepository
{
    private readonly ProductSearchView _view = new();

    public Task<ProductSearchView> GetViewAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_view);
    }

    public Task SaveAsync(ProductSearchView view, CancellationToken cancellationToken = default)
    {
        // No-op in memory; could clone or log
        return Task.CompletedTask;
    }
}
