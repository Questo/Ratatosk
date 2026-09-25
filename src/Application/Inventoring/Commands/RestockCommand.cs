using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Application.Inventoring.Commands;

public sealed record RestockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public class RestockCommandHandler(IInventoryDomainService domainService)
    : IRequestHandler<RestockCommand, Result>
{
    public Task<Result> HandleAsync(
        RestockCommand request,
        CancellationToken cancellationToken = default
    ) => domainService.RestockProductAsync(request.ProductId, request.Quantity, cancellationToken);
}
