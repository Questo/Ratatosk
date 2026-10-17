using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record ReserveStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class ReserveStockCommandHandler(StockKeeper stock)
    : IRequestHandler<ReserveStockCommand, Result>
{
    public Task<Result> HandleAsync(
        ReserveStockCommand request,
        CancellationToken cancellationToken = default
    ) => stock.ReserveAsync(request.ProductId, request.Quantity, cancellationToken);
}
