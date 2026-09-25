using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record ReserveStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class ReserveStockCommandHandler(IInventoryDomainService domainService)
    : IRequestHandler<ReserveStockCommand, Result>
{
    public Task<Result> HandleAsync(
        ReserveStockCommand request,
        CancellationToken cancellationToken = default
    ) => domainService.ReserveProductAsync(request.ProductId, request.Quantity, cancellationToken);
}
