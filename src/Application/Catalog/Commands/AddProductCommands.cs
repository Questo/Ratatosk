using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.Application.Catalog.Commands;

public sealed record AddProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price
) : ICommand;

public class AddProductCommandHandler(IAggregateRepository<Product> repository, IEventBus eventBus) : IHandler<AddProductCommand>
{
    private readonly IAggregateRepository<Product> _repository = repository;
    private readonly IEventBus _eventBus = eventBus;

    public async Task<Result> HandleAsync(AddProductCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = Product.Create(command.Name, command.Sku, command.Description, command.Price);

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
