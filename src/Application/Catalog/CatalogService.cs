using Ratatosk.Domain.Catalog;
using Ratatosk.Core.Primitives;
using Ratatosk.Core.Abstractions;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Application.Catalog;

public interface ICatalogService
{
    Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
}

public class CatalogService(Dispatcher dispatcher) : ICatalogService
{
    public async Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchCommandAsync(command, cancellationToken);
        return result;
    }

    public async Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchCommandAsync(command, cancellationToken);
        return result;
    }
}
