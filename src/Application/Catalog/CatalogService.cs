using Ratatosk.Domain.Catalog;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure;
using Ratatosk.Application.Commands;

namespace Ratatosk.Application.Services;

public class CatalogService(IAggregateRepository<Product> repository, EventBus eventBus)
{
    private readonly IAggregateRepository<Product> _repository = repository;
    private readonly EventBus _eventBus = eventBus;

    public async Task<Result<Product>> HandleAsync(AddProductCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = new ProductBuilder()
                .WithName(command.Name)
                .WithSku(command.Sku)
                .WithDescription(command.Description)
                .WithPrice(command.Price)
                .Build();

            await _repository.SaveAsync(product, cancellationToken);

            foreach (var domainEvent in product.UncommittedEvents)
                await _eventBus.PublishAsync(domainEvent, cancellationToken);

            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            return Result<Product>.Failure(Error.FromException(ex).Message);
        }
    }

    public async Task<Result> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _repository.LoadAsync(command.ProductId, cancellationToken);
        if (result.IsFailure)
            return Result.Failure(result.Error!);

        var product = result.Value!;
        await _repository.SaveAsync(product, cancellationToken);

        foreach (var domainEvent in product.UncommittedEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        return Result.Success();
    }
}
