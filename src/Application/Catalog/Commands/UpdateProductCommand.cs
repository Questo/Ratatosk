using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Application.Catalog.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal? Price
) : IRequest<Result>;

public class UpdateProductCommandHandler(
    IAggregateRepository<Product> repository,
    IEventBus eventBus)
    : IRequestHandler<UpdateProductCommand, Result>
{
    public async Task<Result> HandleAsync(UpdateProductCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            var nameResult = ProductName.Create(request.Name);
            if (nameResult.IsFailure)
                return Result.Failure(nameResult.Error!);

            var descriptionResult = request.Description is null
                ? Result<Description?>.Success(null)!
                : Description.Create(request.Description);

            var priceResult = request.Price is null
                ? Result<Price?>.Success(null)!
                : Price.Create(request.Price.Value);

            var productResult = await repository.LoadAsync(request.ProductId, cancellationToken);
            if (productResult.IsFailure)
                return Result.Failure(productResult.Error!);

            var product = productResult.Value!;
            product.Update(nameResult.Value!, descriptionResult.Value, priceResult.Value);

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
