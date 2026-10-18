using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Changes;

/// <summary>
/// Publishes a <see cref="ChangeSignal"/> for every event that changes the Product, stock or Order
/// read model. Registered after every other handler, so the read model is already written when the
/// signal goes out.
/// </summary>
public class ChangeSignalHandler(IChangeBroadcaster broadcaster)
    : IDomainEventHandler<ProductCreated>,
        IDomainEventHandler<ProductUpdated>,
        IDomainEventHandler<ProductRemoved>,
        IDomainEventHandler<StockAdded>,
        IDomainEventHandler<StockReserved>,
        IDomainEventHandler<StockReleased>,
        IDomainEventHandler<StockRemoved>,
        IDomainEventHandler<OrderCreated>,
        IDomainEventHandler<OrderConfirmed>,
        IDomainEventHandler<OrderCancelled>
{
    public const string Product = "product";
    public const string Stock = "stock";
    public const string Order = "order";

    public Task WhenAsync(ProductCreated domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Product, domainEvent.ProductId, domainEvent.Version);

    public Task WhenAsync(ProductUpdated domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Product, domainEvent.ProductId, domainEvent.Version);

    public Task WhenAsync(ProductRemoved domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Product, domainEvent.ProductId, domainEvent.Version);

    public Task WhenAsync(StockAdded domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Stock, domainEvent.InventoryId, domainEvent.Version);

    public Task WhenAsync(StockReserved domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Stock, domainEvent.InventoryId, domainEvent.Version);

    public Task WhenAsync(StockReleased domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Stock, domainEvent.InventoryId, domainEvent.Version);

    public Task WhenAsync(StockRemoved domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Stock, domainEvent.InventoryId, domainEvent.Version);

    public Task WhenAsync(OrderCreated domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Order, domainEvent.OrderId, domainEvent.Version);

    public Task WhenAsync(OrderConfirmed domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Order, domainEvent.OrderId, domainEvent.Version);

    public Task WhenAsync(OrderCancelled domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(Order, domainEvent.OrderId, domainEvent.Version);

    private Task PublishAsync(string kind, Guid id, int version)
    {
        broadcaster.Publish(new ChangeSignal(kind, id, version));
        return Task.CompletedTask;
    }
}
