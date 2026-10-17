using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.UnitTests.Domain.Inventoring;

[TestClass]
public class InventoryTests
{
    private static Inventory NewInventory() => Inventory.Create(Guid.NewGuid());

    private static SKU NewSku() => SKU.Create(SkuGenerator.Generate("TS")).Value!;

    [TestMethod]
    public void Restock_ShouldRaiseStockAddedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var quantity = Quantity.Pieces(10);

        inventory.Restock(sku, quantity);

        var @event = inventory.UncommittedEvents.OfType<StockAdded>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void Restock_WithDifferentUnit_ShouldFailWithoutRaisingEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));

        var result = inventory.Restock(sku, Quantity.Kilograms(10));

        Assert.IsTrue(result.IsFailure);
        Assert.HasCount(2, inventory.UncommittedEvents);
    }

    [TestMethod]
    public void Restock_FirstRestockInAnotherUnit_SetsTheUnit()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        var result = inventory.Restock(sku, Quantity.Kilograms(3));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("kg", inventory.StockOf(sku).Unit);
        Assert.IsTrue(inventory.RemoveStock(sku, Quantity.Kilograms(3)).IsSuccess);
    }

    [TestMethod]
    public void StockOf_SplitsOnHandIntoReservedAndAvailable()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(3));

        var stock = inventory.StockOf(sku);
        Assert.AreEqual(10, stock.OnHand);
        Assert.AreEqual(3, stock.Reserved);
        Assert.AreEqual(7, stock.Available);
    }

    [TestMethod]
    public void StockOf_UnknownSku_IsEmpty()
    {
        var stock = NewInventory().StockOf(NewSku());

        Assert.AreEqual(0, stock.OnHand);
        Assert.AreEqual(0, stock.Reserved);
    }

    [TestMethod]
    public void ReserveStock_ShouldRaiseStockReservedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5));

        var @event = inventory.UncommittedEvents.OfType<StockReserved>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(5, @event.Quantity);
    }

    [TestMethod]
    public void ReserveStock_WithZeroQuantity_ShouldFail()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));

        Assert.IsTrue(inventory.ReserveStock(sku, Quantity.Pieces(0)).IsFailure);
    }

    [TestMethod]
    public void ReserveStock_InAnotherUnit_ShouldFail()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));

        Assert.IsTrue(inventory.ReserveStock(sku, Quantity.Kilograms(1)).IsFailure);
        Assert.IsEmpty(inventory.UncommittedEvents.OfType<StockReserved>());
    }

    [TestMethod]
    public void ReserveStock_WithInsufficientStock_ShouldFailWithoutRecordingAnything()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(3));

        var result = inventory.ReserveStock(sku, Quantity.Pieces(5));

        Assert.IsTrue(result.IsFailure);
        Assert.IsEmpty(inventory.UncommittedEvents.OfType<StockReservationFailed>());
    }

    [TestMethod]
    public void ReleaseStock_ShouldRaiseStockReleasedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5));
        inventory.ReleaseStock(sku, Quantity.Pieces(5));

        var @event = inventory.UncommittedEvents.OfType<StockReleased>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(5, @event.Quantity);
    }

    [TestMethod]
    public void ReleaseStock_WithZeroQuantity_ShouldFail()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));

        Assert.IsTrue(inventory.ReleaseStock(sku, Quantity.Pieces(0)).IsFailure);
    }

    [TestMethod]
    public void ReleaseStock_InAnotherUnit_ShouldFail()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5));

        Assert.IsTrue(inventory.ReleaseStock(sku, Quantity.Kilograms(5)).IsFailure);
    }

    [TestMethod]
    public void RemoveStock_ShouldRaiseStockRemovedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var quantity = Quantity.Pieces(5);

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.RemoveStock(sku, quantity);

        var @event = inventory.UncommittedEvents.OfType<StockRemoved>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(quantity, @event.Quantity);
    }

    [TestMethod]
    public void RemoveStock_MoreThanAvailable_ShouldFail()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(5));

        Assert.IsTrue(inventory.RemoveStock(sku, Quantity.Pieces(6)).IsFailure);
    }

    [TestMethod]
    public void RemoveStock_WhenSkuNotFound_ShouldFail()
    {
        Assert.IsTrue(NewInventory().RemoveStock(NewSku(), Quantity.Pieces(5)).IsFailure);
    }

    [TestMethod]
    public void RemoveStock_CannotRemoveReservedStock()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(8), Guid.NewGuid());

        Assert.IsTrue(inventory.RemoveStock(sku, Quantity.Pieces(3)).IsFailure);
    }

    [TestMethod]
    public void Create_WithProductId_ShouldUseProductIdAsAggregateId()
    {
        var productId = Guid.NewGuid();

        var inventory = Inventory.Create(productId);

        Assert.AreEqual(productId, inventory.Id);
    }

    [TestMethod]
    public void LoadFromHistory_ShouldRestoreOriginalAggregateId()
    {
        var productId = Guid.NewGuid();
        var created = Inventory.Create(productId);

        var loaded = new Inventory();
        loaded.LoadFromHistory(created.UncommittedEvents);

        Assert.AreEqual(productId, loaded.Id);
    }

    [TestMethod]
    public void IsInStock_WithEnoughAvailableStock_ShouldReturnTrue()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(3));

        Assert.IsTrue(inventory.IsInStock(sku, 7));
    }

    [TestMethod]
    public void IsInStock_WithMoreThanAvailableStock_ShouldReturnFalse()
    {
        var inventory = NewInventory();
        var sku = NewSku();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(3));

        Assert.IsFalse(inventory.IsInStock(sku, 8));
    }

    [TestMethod]
    public void IsInStock_WhenSkuNotFound_ShouldReturnFalse()
    {
        Assert.IsFalse(NewInventory().IsInStock(NewSku(), 1));
    }

    [TestMethod]
    public void ReserveStock_WithOrderId_ShouldRaiseStockReservedEventWithOrderId()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderId = Guid.NewGuid();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5), orderId);

        var @event = inventory.UncommittedEvents.OfType<StockReserved>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(orderId, @event.OrderId);
    }

    [TestMethod]
    public void ReserveStock_WithOrderIdAndInsufficientStock_ShouldRaiseStockReservationFailedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderId = Guid.NewGuid();

        inventory.Restock(sku, Quantity.Pieces(3));
        inventory.ReserveStock(sku, Quantity.Pieces(5), orderId);

        var @event = inventory
            .UncommittedEvents.OfType<StockReservationFailed>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(inventory.Id, @event.InventoryId);
        Assert.AreEqual(sku, @event.SKU);
        Assert.AreEqual(orderId, @event.OrderId);
        Assert.AreEqual(5, @event.Quantity);
        Assert.IsFalse(inventory.UncommittedEvents.OfType<StockReserved>().Any());
    }

    [TestMethod]
    public void ReserveStock_WithOrderIdAndUnknownSku_ShouldRaiseStockReservationFailedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderId = Guid.NewGuid();

        inventory.ReserveStock(sku, Quantity.Pieces(5), orderId);

        var @event = inventory
            .UncommittedEvents.OfType<StockReservationFailed>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(orderId, @event.OrderId);
    }

    [TestMethod]
    public void ReserveStock_WithOrderIdInAnotherUnit_ShouldRaiseStockReservationFailedEvent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderId = Guid.NewGuid();
        inventory.Restock(sku, Quantity.Kilograms(10));

        var result = inventory.ReserveStock(sku, Quantity.Pieces(5), orderId);

        Assert.IsTrue(result.IsFailure);
        Assert.HasCount(1, inventory.UncommittedEvents.OfType<StockReservationFailed>());
    }

    [TestMethod]
    public void ReleaseStock_ByOrderId_ShouldReleaseOnlyThatOrdersReservation()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5), orderA);
        inventory.ReserveStock(sku, Quantity.Pieces(3), orderB);
        inventory.ClearUncommittedEvents();

        inventory.ReleaseStock(sku, orderA);

        var @event = inventory.UncommittedEvents.OfType<StockReleased>().FirstOrDefault();
        Assert.IsNotNull(@event);
        Assert.AreEqual(5, @event.Quantity);
        // On hand 10, only order B's reservation (3) remains -> 7 available.
        Assert.AreEqual(7, inventory.StockOf(sku).Available);
    }

    [TestMethod]
    public void ReleaseStock_ByOrderId_WhenOrderNeverReserved_ShouldBeSafeNoOp()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5), orderA);
        inventory.ClearUncommittedEvents();

        inventory.ReleaseStock(sku, orderB);

        Assert.IsFalse(inventory.UncommittedEvents.OfType<StockReleased>().Any());
        Assert.AreEqual(5, inventory.StockOf(sku).Available);
    }

    [TestMethod]
    public void ReleaseStock_ByOrderId_CalledTwice_ShouldBeIdempotent()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderA = Guid.NewGuid();

        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(5), orderA);
        inventory.ClearUncommittedEvents();

        inventory.ReleaseStock(sku, orderA);
        inventory.ReleaseStock(sku, orderA);

        Assert.AreEqual(1, inventory.UncommittedEvents.OfType<StockReleased>().Count());
        Assert.AreEqual(10, inventory.StockOf(sku).Available);
    }

    [TestMethod]
    public void ReleaseStock_ByHand_CannotReleaseStockHeldByAnOrder()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(2));
        inventory.ReserveStock(sku, Quantity.Pieces(5), Guid.NewGuid());

        Assert.IsTrue(inventory.ReleaseStock(sku, Quantity.Pieces(3)).IsFailure);
        Assert.IsTrue(inventory.ReleaseStock(sku, Quantity.Pieces(2)).IsSuccess);
        Assert.AreEqual(5, inventory.StockOf(sku).Reserved, "the order's 5 units must stay reserved");
    }

    [TestMethod]
    public void ReserveStock_ForAnOrderThatAlreadyHoldsTheSku_IsANoOp()
    {
        var inventory = NewInventory();
        var sku = NewSku();
        var orderId = Guid.NewGuid();
        inventory.Restock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, Quantity.Pieces(4), orderId);

        var again = inventory.ReserveStock(sku, Quantity.Pieces(4), orderId);

        Assert.IsTrue(again.IsSuccess);
        Assert.HasCount(1, inventory.UncommittedEvents.OfType<StockReserved>());
    }
}
