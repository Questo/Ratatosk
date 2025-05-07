using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.UnitTests;

[TestClass]
public class InventoryTests
{
    [TestMethod]
    public void AddStock_ShouldRaiseStockAddedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = 10;

        inventory.AddStock(sku, quantity);

        var @event = inventory.UncommittedEvents
            .OfType<StockAdded>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void AddStock_WithNegativeQuantity_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = -5;

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => inventory.AddStock(sku, quantity));
    }

    [TestMethod]
    public void ReserveStock_ShouldRaiseStockReservedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = 5;

        inventory.AddStock(sku, 10);
        inventory.ReserveStock(sku, quantity);

        var @event = inventory.UncommittedEvents
            .OfType<StockReserved>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void ReserveStock_WithNegativeQuantity_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = -5;

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => inventory.ReserveStock(sku, quantity));
    }

    [TestMethod]
    public void ReserveStock_WithInsufficientStock_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = 5;

        inventory.AddStock(sku, 3);

        Assert.ThrowsException<InvalidOperationException>(() => inventory.ReserveStock(sku, quantity));
    }
}