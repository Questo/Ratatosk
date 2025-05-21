using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Catalog.Queries;

public sealed record SearchProductsQuery(string? SearchTerm = null, int Page = 1, int PageSize = 25) : IRequest<Result<IEnumerable<ProductReadModel>>>;

public class SearchProductsQueryHandler(IProductReadModelRepository repository) : IRequestHandler<SearchProductsQuery, Result<IEnumerable<ProductReadModel>>>
{
    public async Task<Result<IEnumerable<ProductReadModel>>> HandleAsync(SearchProductsQuery request, CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await repository.GetAllAsync(request.SearchTerm, request.Page, request.PageSize, cancellationToken);
            return Result<IEnumerable<ProductReadModel>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ProductReadModel>>.Failure(ex.Message);
        }
    }
}
