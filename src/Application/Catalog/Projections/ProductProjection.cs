using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Application.Catalog.Projections;

public class ProductProjection(IProductReadModelRepository repo) :
    IProjection<ProductCreated>,
    IProjection<ProductUpdated>
{
    public async Task WhenAsync(ProductCreated domainEvent, CancellationToken cancellationToken)
    {
        var readModel = new ProductReadModel
        {
            Id = domainEvent.ProductId,
            Name = domainEvent.Name,
            Price = domainEvent.Price.Amount,
            Description = domainEvent.Description,
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(ProductUpdated domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByIdAsync(domainEvent.ProductId, cancellationToken);
        if (existing == null) return;

        existing.Name = domainEvent.Name;
        existing.Description = domainEvent.Description;
        existing.Price = domainEvent.Price.Amount;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }
}