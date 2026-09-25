using Ratatosk.Core.BuildingBlocks;
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

        var @event = inventory.UncommittedEvents.OfType<StockAdded>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void AddStock_WithDifferentUnit_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(10);

        inventory.AddStock(sku, quantity);

        Assert.Throws<ArgumentException>(() =>
            inventory.AddStock(sku, Quantity.Create(10, "kg").Value!)
        );
    }

    [TestMethod]
    public void ReserveStock_ShouldRaiseStockReservedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(5);

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, quantity.Amount);

        var @event = inventory.UncommittedEvents.OfType<StockReserved>().FirstOrDefault();

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

        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.ReserveStock(sku, quantity));
    }

    [TestMethod]
    public void ReserveStock_WithInsufficientStock_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = 5;

        inventory.AddStock(sku, Quantity.Pieces(3));

        Assert.Throws<InvalidOperationException>(() => inventory.ReserveStock(sku, quantity));
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

        var @event = inventory.UncommittedEvents.OfType<StockReleased>().FirstOrDefault();

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

        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.ReleaseStock(sku, quantity));
    }

    [TestMethod]
    public void RemoveStock_ShouldRaiseStockRemovedEvent()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(5);

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.RemoveStock(sku, quantity);

        var @event = inventory.UncommittedEvents.OfType<StockRemoved>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void RemoveStock_WithNegativeQuantity_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(5);

        inventory.AddStock(sku, quantity);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.RemoveStock(sku, Quantity.Pieces(6))
        );
    }

    [TestMethod]
    public void RemoveStock_WhenSkuNotFound_ShouldThrowException()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var quantity = Quantity.Pieces(5);

        Assert.Throws<InvalidOperationException>(() => inventory.RemoveStock(sku, quantity));
    }

    [TestMethod]
    public void Create_WithProductId_ShouldUseProductIdAsAggregateId()
    {
        var productId = Guid.NewGuid();

        var inventory = Inventory.Create(productId);

        Assert.AreEqual(productId, inventory.Id);
    }

    [TestMethod]
    public void Rehydrate_FromHistory_ShouldRestoreOriginalAggregateId()
    {
        var productId = Guid.NewGuid();
        var created = Inventory.Create(productId);

        var rehydrated = AggregateRoot.Rehydrate<Inventory>(created.UncommittedEvents.Reverse());

        Assert.IsTrue(rehydrated.IsSuccess);
        Assert.AreEqual(productId, rehydrated.Value!.Id);
    }

    [TestMethod]
    public void IsInStock_WithEnoughAvailableStock_ShouldReturnTrue()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3);

        Assert.IsTrue(inventory.IsInStock(sku, 7));
    }

    [TestMethod]
    public void IsInStock_WithMoreThanAvailableStock_ShouldReturnFalse()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;

        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3);

        Assert.IsFalse(inventory.IsInStock(sku, 8));
    }

    [TestMethod]
    public void IsInStock_WhenSkuNotFound_ShouldReturnFalse()
    {
        var inventory = Inventory.Create();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;

        Assert.IsFalse(inventory.IsInStock(sku, 1));
    }
}
