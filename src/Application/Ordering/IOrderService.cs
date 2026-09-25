using Microsoft.Extensions.Logging;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Ordering.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering;

public interface IOrderService
{
    Task<Result<Guid>> PlaceOrderAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default
    );
    Task<Result<OrderReadModel>> GetOrderByIdAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    );
}

public class OrderService(IDispatcher dispatcher, IUnitOfWork uow, ILogger<OrderService> logger)
    : IOrderService
{
    public async Task<Result<Guid>> PlaceOrderAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to place order: {Error}", result.Error);
        else
            uow.Commit();

        return result;
    }

    public async Task<Result<OrderReadModel>> GetOrderByIdAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(query, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to fetch order: {Error}", result.Error);

        return result;
    }
}
