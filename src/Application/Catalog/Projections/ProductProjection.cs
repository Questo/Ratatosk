using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Application.Catalog.Projections;

public class ProductProjection(IProductReadModelRepository repo) :
    IDomainEventHandler<ProductCreated>,
    IDomainEventHandler<ProductUpdated>,
    IDomainEventHandler<ProductRemoved>
{
    public async Task WhenAsync(ProductCreated domainEvent, CancellationToken cancellationToken)
    {
        var readModel = new ProductReadModel
        {
            Id = domainEvent.ProductId,
            Name = domainEvent.Name.Value,
            Sku = domainEvent.Sku.Value,
            Price = domainEvent.Price.Amount,
            Description = domainEvent.Description.Value,
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(ProductUpdated domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByIdAsync(domainEvent.ProductId, cancellationToken);
        if (existing == null) return;

        existing.Name = domainEvent.Name.Value;
        existing.Description = domainEvent.Description.Value;
        existing.Price = domainEvent.Price.Amount;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(ProductRemoved domainEvent, CancellationToken cancellationToken = default)
    {
        await repo.DeleteAsync(domainEvent.ProductId, cancellationToken);
    }
}