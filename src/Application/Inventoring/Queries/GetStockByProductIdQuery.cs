using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Queries;

public sealed record GetStockByProductIdQuery(Guid ProductId) : IRequest<Result<StockReadModel>>;

public class GetStockByProductIdQueryHandler(IInventoryReadModelRepository repository)
    : IRequestHandler<GetStockByProductIdQuery, Result<StockReadModel>>
{
    public async Task<Result<StockReadModel>> HandleAsync(
        GetStockByProductIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await repository.GetByProductIdAsync(query.ProductId, cancellationToken);
        if (stock is null)
            return Result<StockReadModel>.Failure($"No stock record found for product {query.ProductId}");

        return Result<StockReadModel>.Success(stock);
    }
}
