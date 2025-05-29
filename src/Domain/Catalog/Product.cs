using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog;

public class Product : AggregateRoot
{
    public ProductName Name { get; private set; } = default!;
    public SKU Sku { get; private set; } = default!;
    public Description Description { get; private set; } = default!;
    public Price Price { get; private set; } = default!;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ProductCreated e:
                Id = e.ProductId;
                Name = e.Name;
                Sku = e.Sku;
                Description = e.Description;
                Price = e.Price;
                break;

            case ProductUpdated e:
                Name = e.Name;
                Description = e.Description;
                Price = e.Price;
                break;
        }
    }

    public override Snapshot? CreateSnapshot() => new ProductSnapshot
    {
        AggregateId = Id,
        Version = Version,
        AggregateType = GetType().FullName!,
        Name = Name,
        Sku = Sku,
        Description = Description,
        Price = Price
    };

    public static Product Create(string name, string sku, string description, decimal price)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNullOrEmpty(sku, nameof(sku));
        Guard.AgainstNullOrEmpty(description, nameof(description));

        var nameResult = ProductName.Create(name);
        if (nameResult.IsFailure)
            throw new ArgumentException(nameResult.Error);

        var skuResult = SKU.Create(sku);
        if (skuResult.IsFailure)
            throw new ArgumentException(skuResult.Error);

        var descriptionResult = Description.Create(description);
        if (descriptionResult.IsFailure)
            throw new ArgumentException(descriptionResult.Error);

        var priceResult = Price.Create(price);
        if (priceResult.IsFailure)
            throw new ArgumentException(priceResult.Error);

        var product = new Product();

        product.RaiseEvent(new ProductCreated(product.Id, nameResult.Value!, skuResult.Value!, descriptionResult.Value!, priceResult.Value!));

        return product;
    }

    public void Update(ProductName name, Description? description, Price? price)
    {
        bool nameChanged = !Name.Equals(name);
        bool descChanged = description != null && !Description.Equals(description);
        bool priceChanged = price != null && !Price.Equals(price);

        bool productChanged = nameChanged || descChanged || priceChanged;

        if (!productChanged)
        {
            return;
        }

        ProductUpdated @event = new(Id, name, description, price);
        RaiseEvent(@event);
    }

    public void Remove(string? reason = null)
    {
        RaiseEvent(new ProductRemoved(Id, reason));
    }
}
