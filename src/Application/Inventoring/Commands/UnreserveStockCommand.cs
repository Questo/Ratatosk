using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record UnreserveStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class UnreserveStockCommandHandler(IInventoryDomainService domainService)
    : IRequestHandler<UnreserveStockCommand, Result>
{
    public Task<Result> HandleAsync(
        UnreserveStockCommand request,
        CancellationToken cancellationToken = default
    ) => domainService.UnreserveProductAsync(request.ProductId, request.Quantity, cancellationToken);
}
