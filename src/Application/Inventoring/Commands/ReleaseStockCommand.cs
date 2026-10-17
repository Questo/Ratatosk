using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record ReleaseStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class ReleaseStockCommandHandler(StockKeeper stock)
    : IRequestHandler<ReleaseStockCommand, Result>
{
    public Task<Result> HandleAsync(
        ReleaseStockCommand request,
        CancellationToken cancellationToken = default
    ) => stock.ReleaseAsync(request.ProductId, request.Quantity, cancellationToken);
}
