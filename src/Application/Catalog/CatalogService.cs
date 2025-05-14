using Ratatosk.Core.Primitives;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Core.BuildingBlocks;
using Microsoft.Extensions.Logging;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Catalog.Queries;

namespace Ratatosk.Application.Catalog;

public interface ICatalogService
{
    Task<Result<Guid>> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveProductAsync(RemoveProductCommand command, CancellationToken cancellationToken = default);
    Task<Result<ProductReadModel>> GetProductByIdAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default);
}

public class CatalogService(IDispatcher dispatcher, ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<Result<Guid>> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to create product: {Error}", result.Error);
        }

        return result;
    }

    public async Task<Result<ProductReadModel>> GetProductByIdAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to fetch product: {Error}", result.Error);
        }

        return result;
    }

    public async Task<Result> RemoveProductAsync(RemoveProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to remove product: {Error}", result.Error);
        }
        return result;
    }

    public async Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to update product: {Error}", result.Error);
        }

        return result;
    }
}
