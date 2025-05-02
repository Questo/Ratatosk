using Ratatosk.Core.Primitives;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Core.BuildingBlocks;
using Microsoft.Extensions.Logging;

namespace Ratatosk.Application.Catalog;

public interface ICatalogService
{
    Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
}

public class CatalogService(Dispatcher dispatcher, ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<Result> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchCommandAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to create product: {Error}", result.Error);
        }

        return result;
    }

    public async Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchCommandAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to update product: {Error}", result.Error);
        }

        return result;
    }
}
