using Ratatosk.Domain.Catalog;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure;
using Ratatosk.Application.Commands;

namespace Ratatosk.Application.Catalog;

public interface ICatalogService
{
    Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
}

public class CatalogService(IAggregateRepository<Product> repository, EventBus eventBus) : ICatalogService
{
    private readonly IAggregateRepository<Product> _repository = repository;
    private readonly EventBus _eventBus = eventBus;

    public async Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default)
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

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.FromException(ex).Message);
        }
    }

    public async Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.LoadAsync(command.ProductId, cancellationToken);
            if (result.IsFailure)
                return Result.Failure(result.Error!);

            var product = result.Value!;
            var updateResult = product.Update(command.Name, command.Description, Price.Create(command.Price ?? default!).Value);
            if (updateResult.IsFailure)
                return Result.Failure(result.Error!);

            await _repository.SaveAsync(product, cancellationToken);

            foreach (var domainEvent in product.UncommittedEvents)
                await _eventBus.PublishAsync(domainEvent, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.FromException(ex).Message);
        }
    }
}
