using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Application.Catalog.Models;

public class ProductReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public DateTime LastUpdatedUtc { get; set; }

    public ProductReadModel() { }

    public ProductReadModel(
        Guid id,
        string name,
        string sku,
        string description,
        decimal price,
        DateTime lastUpdatedUtc
    )
    {
        Id = id;
        Name = name;
        Sku = sku;
        Description = description;
        Price = price;
        LastUpdatedUtc = lastUpdatedUtc;
    }
}

public class ProductProjection(IProductReadModelRepository repo)
    : IDomainEventHandler<ProductCreated>,
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
            LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime,
        };

        await repo.SaveAsync(readModel, cancellationToken);
    }

    public async Task WhenAsync(ProductUpdated domainEvent, CancellationToken cancellationToken)
    {
        var existing = await repo.GetByIdAsync(domainEvent.ProductId, cancellationToken);
        if (existing == null)
            return;

        if (domainEvent.Name is not null)
            existing.Name = domainEvent.Name.Value;
        if (domainEvent.Description is not null)
            existing.Description = domainEvent.Description.Value;
        if (domainEvent.Price is not null)
            existing.Price = domainEvent.Price.Amount;
        existing.LastUpdatedUtc = domainEvent.OccurredAtUtc.UtcDateTime;

        await repo.SaveAsync(existing, cancellationToken);
    }

    public async Task WhenAsync(
        ProductRemoved domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await repo.DeleteAsync(domainEvent.ProductId, cancellationToken);
    }
}
