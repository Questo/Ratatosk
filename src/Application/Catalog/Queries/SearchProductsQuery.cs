using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Catalog.Queries;

public sealed record SearchProductsQuery(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 25,
    decimal? MinPrice = null,
    decimal? MaxPrice = null
) : IRequest<Result<Pagination<ProductReadModel>>>;

public class SearchProductsQueryHandler(IProductReadModelRepository repository)
    : IRequestHandler<SearchProductsQuery, Result<Pagination<ProductReadModel>>>
{
    public async Task<Result<Pagination<ProductReadModel>>> HandleAsync(
        SearchProductsQuery request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.MinPrice < 0)
            return Result<Pagination<ProductReadModel>>.Failure("minPrice cannot be negative");

        if (request.MaxPrice < 0)
            return Result<Pagination<ProductReadModel>>.Failure("maxPrice cannot be negative");

        if (request.MinPrice > request.MaxPrice)
            return Result<Pagination<ProductReadModel>>.Failure(
                "minPrice cannot be greater than maxPrice"
            );

        try
        {
            var products = await repository.GetAllAsync(
                request.SearchTerm,
                request.Page,
                request.PageSize,
                request.MinPrice,
                request.MaxPrice,
                cancellationToken
            );
            return Result<Pagination<ProductReadModel>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<Pagination<ProductReadModel>>.Failure(ex.Message);
        }
    }
}
