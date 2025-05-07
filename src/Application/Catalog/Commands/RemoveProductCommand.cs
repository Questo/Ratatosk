using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.Application.Catalog.Commands;

public sealed record RemoveProductCommand(Guid ProductId) : IRequest<Result>;

public class RemoveProductCommandHandler(
    IAggregateRepository<Product> repository,
    IEventBus eventBus)
    : IRequestHandler<RemoveProductCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveProductCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await repository.LoadAsync(request.ProductId, cancellationToken);
            if (result.IsFailure)
                return Result.Failure(result.Error!);

            var product = result.Value!;
            product.Remove();

            await repository.SaveAsync(product, cancellationToken);

            foreach (var domainEvent in product.UncommittedEvents)
                await eventBus.PublishAsync(domainEvent, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.FromException(ex).Message);
        }
    }
}
