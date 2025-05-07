using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Inventoring;

public sealed class InventoryUpdated(string sku, int quantity) : DomainEvent
{
    public string Sku { get; } = sku;
    public int Quantity { get; } = quantity;
}

public class Inventory : AggregateRoot
{
    private readonly Dictionary<string, int> _products = [];

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case InventoryUpdated e:
                if (_products.ContainsKey(e.Sku))
                {
                    _products[e.Sku] = e.Quantity;
                }
                else
                {
                    _products.Add(e.Sku, e.Quantity);
                }
                break;
        }
    }

    public void Add(string sku, int quantity)
    {
        Guard.AgainstNullOrEmpty(sku, nameof(sku));
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        var @event = new InventoryUpdated(
            sku,
            quantity: !_products.TryGetValue(sku, out int value)
                ? quantity
                : value + quantity);

        RaiseEvent(@event);
    }

    public void Update(string productSku, int quantity)
    {
        // Logic to update the product in inventory
        // This is a placeholder for the actual implementation
        throw new NotImplementedException();
    }
    public void Reserve(string productSku, int quantity)
    {
        // Logic to reserve the product in inventory
        // This is a placeholder for the actual implementation
        throw new NotImplementedException();
    }
    public void Unreserve(string productSku, int quantity)
    {
        // Logic to unreserve the product in inventory
        // This is a placeholder for the actual implementation
        throw new NotImplementedException();
    }
    public void Restock(string productSku, int quantity)
    {
        // Logic to restock the product in inventory
        // This is a placeholder for the actual implementation
        throw new NotImplementedException();
    }
    public void Remove(string productSku)
    {
        // Logic to remove the product from inventory
        // This is a placeholder for the actual implementation
        throw new NotImplementedException();
    }
}