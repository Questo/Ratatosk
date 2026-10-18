using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Queries;

/// <summary>
/// Every Stock record, removed Products' included, ordered by SKU. With <see cref="MaxAvailable"/>,
/// only records with at most that many available, lowest available first.
/// </summary>
public sealed record GetStocksQuery(int Page = 1, int PageSize = 25, int? MaxAvailable = null)
    : IRequest<Result<Pagination<StockReadModel>>>;

public class GetStocksQueryHandler(IInventoryReadModelRepository repository)
    : IRequestHandler<GetStocksQuery, Result<Pagination<StockReadModel>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<Pagination<StockReadModel>>> HandleAsync(
        GetStocksQuery request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.Page < 1)
            return Result<Pagination<StockReadModel>>.Failure("Page must be at least 1.");

        if (request.PageSize is < 1 or > MaxPageSize)
            return Result<Pagination<StockReadModel>>.Failure(
                $"Page size must be between 1 and {MaxPageSize}."
            );

        if (request.MaxAvailable < 0)
            return Result<Pagination<StockReadModel>>.Failure("Max available must be at least 0.");

        var stocks = await repository.GetPageAsync(
            request.Page,
            request.PageSize,
            request.MaxAvailable,
            cancellationToken
        );
        return Result<Pagination<StockReadModel>>.Success(stocks);
    }
}
