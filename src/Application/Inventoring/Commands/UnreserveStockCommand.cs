using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record UnreserveStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class UnreserveStockCommandHandler(StockKeeper stock)
    : IRequestHandler<UnreserveStockCommand, Result>
{
    public Task<Result> HandleAsync(
        UnreserveStockCommand request,
        CancellationToken cancellationToken = default
    ) => stock.ReleaseAsync(request.ProductId, request.Quantity, cancellationToken);
}
