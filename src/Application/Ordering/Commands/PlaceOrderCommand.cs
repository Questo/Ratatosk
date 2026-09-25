using Microsoft.Extensions.Logging;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering.Commands;

public sealed record OrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineRequest> Lines)
    : IRequest<Result<Guid>>;

public class PlaceOrderCommandHandler(
    IInventoryReadModelRepository inventoryReadModelRepository,
    IProductReadModelRepository productReadModelRepository,
    IAggregateRepository<Order> repository,
    IOrderReadModelRepository orderReadModelRepository,
    IEventBus eventBus,
    IUnitOfWork uow,
    ILogger<PlaceOrderCommandHandler> logger
) : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(
        PlaceOrderCommand request,
        CancellationToken cancellationToken = default
    )
    {
        var lines = new List<OrderLine>();

        foreach (var lineRequest in request.Lines)
        {
            var skuResult = SKU.Create(lineRequest.Sku);
            if (skuResult.IsFailure)
                return Result<Guid>.Failure(skuResult.Error!);

            var stockReadModel = await inventoryReadModelRepository.GetBySkuAsync(
                lineRequest.Sku,
                cancellationToken
            );
            if (stockReadModel is null)
                return Result<Guid>.Failure($"SKU {lineRequest.Sku} not found");

            var productReadModel = await productReadModelRepository.GetByIdAsync(
                stockReadModel.ProductId,
                cancellationToken
            );
            if (productReadModel is null)
                return Result<Guid>.Failure($"Product for SKU {lineRequest.Sku} not found");

            var priceResult = Price.Create(productReadModel.Price);
            if (priceResult.IsFailure)
                return Result<Guid>.Failure(priceResult.Error!);

            var lineResult = OrderLine.Create(
                skuResult.Value!,
                lineRequest.Quantity,
                priceResult.Value!
            );
            if (lineResult.IsFailure)
                return Result<Guid>.Failure(lineResult.Error!);

            lines.Add(lineResult.Value!);
        }

        var orderResult = Order.Place(request.CustomerId, lines);
        if (orderResult.IsFailure)
            return Result<Guid>.Failure(orderResult.Error!);

        var order = orderResult.Value!;
        await repository.SaveAsync(order, cancellationToken);

        var readModel = new OrderReadModel
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            Lines =
            [
                .. order.Lines.Select(l => new OrderLineReadModel(
                    l.Sku.Value,
                    l.Quantity,
                    l.UnitPrice.Amount,
                    l.UnitPrice.Currency
                )),
            ],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
        };
        await orderReadModelRepository.SaveAsync(readModel, cancellationToken);

        // Committed here, before publishing: cascading handlers open their own DB transaction
        // each, and would otherwise race this one and either miss the order or deadlock on it.
        uow.Commit();

        try
        {
            foreach (var raised in order.UncommittedEvents)
                await eventBus.PublishAsync(raised, cancellationToken);
        }
        catch (Exception ex)
        {
            // The order itself is already committed; a failure downstream in the reservation
            // cascade must not turn into a 500 for an order that exists. It's left Created.
            logger.LogError(ex, "Failed to publish events for order {OrderId}", order.Id);
        }

        return Result<Guid>.Success(order.Id);
    }
}
