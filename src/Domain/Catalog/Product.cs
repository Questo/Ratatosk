using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Domain.Catalog;

public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public Price Price { get; private set; } = default!;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ProductAdded e:
                Id = e.ProductId;
                Name = e.Name;
                Sku = e.Sku;
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

    public Result Update(string name, string? description, Price? price)
    {
        try
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));

            bool nameChanged = !Name.Equals(name);
            bool descChanged = !Description.Equals(description);
            bool priceChanged = !Price.Equals(price);

            bool productChanged = nameChanged || descChanged || priceChanged;

            if (!productChanged)
            {
                return Result.Failure("Nothing has changed");
            }

            ProductUpdated @event = new(Id, name, description, price);
            RaiseEvent(@event);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result.Failure(error.Message);
        }
    }
}