using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.Application.Catalog.Commands;

public sealed record AddProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price
) : IRequest<Result<Guid>>;

public class AddProductCommandHandler(
    IAggregateRepository<Product> repository,
    IEventBus eventBus,
    ISkuUniqueness skuUniqueness)
    : IRequestHandler<AddProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(AddProductCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            var skuIsUnique = await skuUniqueness.IsUniqueAsync(request.Sku, cancellationToken);
            if (!skuIsUnique)
                return Result<Guid>.Failure($"SKU {request.Sku} is already in use");

            var product = Product.Create(request.Name, request.Sku, request.Description, request.Price);

            await repository.SaveAsync(product, cancellationToken);

            foreach (var domainEvent in product.UncommittedEvents)
                await eventBus.PublishAsync(domainEvent, cancellationToken);

            return Result<Guid>.Success(product.Id);
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure(Error.FromException(ex).Message);
        }
    }

}
