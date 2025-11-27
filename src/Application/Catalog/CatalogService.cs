using Microsoft.Extensions.Logging;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Catalog;

public interface ICatalogService
{
    Task<Result<Guid>> AddProductAsync(
        AddProductCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result<Pagination<ProductReadModel>>> GetProductsAsync(
        SearchProductsQuery query,
        CancellationToken cancellationToken = default
    );
    Task<Result> UpdateProductAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result> RemoveProductAsync(
        RemoveProductCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result<ProductReadModel>> GetProductByIdAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default
    );
}

public class CatalogService(IDispatcher dispatcher, IUnitOfWork uow, ILogger<CatalogService> logger)
    : ICatalogService
{
    public async Task<Result<Guid>> AddProductAsync(
        AddProductCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to create product: {Error}", result.Error);
        }

        uow.Commit();
        return result;
    }

    public async Task<Result<ProductReadModel>> GetProductByIdAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to fetch product: {Error}", result.Error);
        }

        return result;
    }

    public async Task<Result<Pagination<ProductReadModel>>> GetProductsAsync(
        SearchProductsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to fetch products: {Error}", result.Error);
        }

        return result;
    }

    public async Task<Result> RemoveProductAsync(
        RemoveProductCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to remove product: {Error}", result.Error);
        }

        uow.Commit();
        return result;
    }

    public async Task<Result> UpdateProductAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Failed to update product: {Error}", result.Error);
        }

        uow.Commit();
        return result;
    }
}
