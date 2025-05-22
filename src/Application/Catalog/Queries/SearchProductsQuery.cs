using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Catalog.Queries;

public sealed record SearchProductsQuery(string? SearchTerm = null, int Page = 1, int PageSize = 25) : IRequest<Result<Pagination<ProductReadModel>>>;

public class SearchProductsQueryHandler(IProductReadModelRepository repository) : IRequestHandler<SearchProductsQuery, Result<Pagination<ProductReadModel>>>
{
    public async Task<Result<Pagination<ProductReadModel>>> HandleAsync(SearchProductsQuery request, CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await repository.GetAllAsync(request.SearchTerm, request.Page, request.PageSize, cancellationToken);
            return Result<Pagination<ProductReadModel>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<Pagination<ProductReadModel>>.Failure(ex.Message);
        }
    }
}
