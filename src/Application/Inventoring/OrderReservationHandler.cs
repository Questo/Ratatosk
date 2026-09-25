using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Inventoring;

public class OrderReservationHandler(
    IInventoryReadModelRepository readModelRepository,
    IAggregateRepository<Inventory> repository,
    IEventBus eventBus,
    IUnitOfWork uow
) : IDomainEventHandler<OrderCreated>
{
    public async Task WhenAsync(
        OrderCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var line in domainEvent.Lines)
        {
            var readModel = await readModelRepository.GetBySkuAsync(
                line.Sku.Value,
                cancellationToken
            );
            if (readModel is null)
            {
                // Guid.Empty: no inventory record exists for this SKU, so there's no real id to report.
                await eventBus.PublishAsync(
                    new StockReservationFailed(
                        Guid.Empty,
                        line.Sku,
                        domainEvent.OrderId,
                        line.Quantity,
                        $"SKU {line.Sku} has no inventory record"
                    ),
                    cancellationToken
                );
                // Stop: reserving later lines here would leak stock once this order cancels.
                break;
            }

            var inventoryResult = await repository.LoadAsync(
                readModel.ProductId,
                cancellationToken
            );
            if (inventoryResult.IsFailure)
            {
                await eventBus.PublishAsync(
                    new StockReservationFailed(
                        readModel.ProductId,
                        line.Sku,
                        domainEvent.OrderId,
                        line.Quantity,
                        inventoryResult.Error ?? "Failed to load inventory"
                    ),
                    cancellationToken
                );
                break;
            }

            var inventory = inventoryResult.Value!;
            inventory.ReserveStock(line.Sku, line.Quantity, domainEvent.OrderId);

            var lineFailed = inventory.UncommittedEvents.OfType<StockReservationFailed>().Any();

            await repository.SaveAsync(inventory, cancellationToken);

            // Commit before publishing so nested handlers can read this reservation back.
            uow.Commit();

            foreach (var raised in inventory.UncommittedEvents)
                await eventBus.PublishAsync(raised, cancellationToken);

            if (lineFailed)
                break;
        }
    }
}
