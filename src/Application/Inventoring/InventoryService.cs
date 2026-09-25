using Microsoft.Extensions.Logging;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Inventoring.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Inventoring;

public interface IInventoryService
{
    Task<Result> ReserveStockAsync(
        ReserveStockCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result> UnreserveStockAsync(
        UnreserveStockCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result> RestockAsync(RestockCommand command, CancellationToken cancellationToken = default);
    Task<Result<StockReadModel>> GetStockByProductIdAsync(
        GetStockByProductIdQuery query,
        CancellationToken cancellationToken = default
    );
    Task<Result<StockReadModel>> GetStockBySkuAsync(
        GetStockBySkuQuery query,
        CancellationToken cancellationToken = default
    );
}

public class InventoryService(IDispatcher dispatcher, IUnitOfWork uow, ILogger<InventoryService> logger)
    : IInventoryService
{
    public async Task<Result> ReserveStockAsync(
        ReserveStockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to reserve stock: {Error}", result.Error);
        else
            uow.Commit();

        return result;
    }

    public async Task<Result> UnreserveStockAsync(
        UnreserveStockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to unreserve stock: {Error}", result.Error);
        else
            uow.Commit();

        return result;
    }

    public async Task<Result> RestockAsync(
        RestockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to restock: {Error}", result.Error);
        else
            uow.Commit();

        return result;
    }

    public async Task<Result<StockReadModel>> GetStockByProductIdAsync(
        GetStockByProductIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to fetch stock: {Error}", result.Error);

        return result;
    }

    public async Task<Result<StockReadModel>> GetStockBySkuAsync(
        GetStockBySkuQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to fetch stock: {Error}", result.Error);

        return result;
    }
}
