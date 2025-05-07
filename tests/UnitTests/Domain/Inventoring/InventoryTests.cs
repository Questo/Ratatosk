using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.UnitTests.Domain.Inventoring;

[TestClass]
public class InventoryTests
{
    [TestMethod]
    public void AddStock_ShouldRaiseStockAddedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(10);

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
    public void ReserveStock_ShouldRaiseStockReservedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(5);

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, quantity.Amount);

        var @event = inventory.UncommittedEvents
            .OfType<StockReserved>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity.Amount, @event.Quantity);
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

        inventory.AddStock(sku, Quantity.Pieces(3));

        Assert.ThrowsException<InvalidOperationException>(() => inventory.ReserveStock(sku, quantity));
    }

    [TestMethod]
    public void ReleaseStock_ShouldRaiseStockReleasedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = 5;

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, quantity);
        inventory.ReleaseStock(sku, quantity);

        var @event = inventory.UncommittedEvents
            .OfType<StockReleased>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void ReleaseStock_WithNegativeQuantity_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = -5;

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => inventory.ReleaseStock(sku, quantity));
    }
}