using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record RestockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class RestockCommandHandler(StockKeeper stock)
    : IRequestHandler<RestockCommand, Result>
{
    public Task<Result> HandleAsync(
        RestockCommand request,
        CancellationToken cancellationToken = default
    ) => stock.RestockAsync(request.ProductId, request.Quantity, cancellationToken);
}
