using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.Domain.Inventoring;

public class InventoryBuilder : IBuilder<Inventory>
{
    public Inventory Build()
    {
        var inventory = new Inventory();
        inventory.LoadFromHistory([new InventoryCreated(inventory.Id)]);
        return inventory;
    }

    public IBuilder<Inventory> With<TValue>(string propertyName, TValue value)
    {
        var property = typeof(InventoryBuilder).GetField(
            $"_{propertyName}",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        property?.SetValue(this, value);
        return this;
    }
}
