using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Queries;

public sealed record GetStockBySkuQuery(string Sku) : IRequest<Result<StockReadModel>>;

public class GetStockBySkuQueryHandler(IInventoryReadModelRepository repository)
    : IRequestHandler<GetStockBySkuQuery, Result<StockReadModel>>
{
    public async Task<Result<StockReadModel>> HandleAsync(
        GetStockBySkuQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stock = await repository.GetBySkuAsync(query.Sku, cancellationToken);
        if (stock is null)
            return Result<StockReadModel>.Failure($"No stock record found for SKU {query.Sku}");

        return Result<StockReadModel>.Success(stock);
    }
}
