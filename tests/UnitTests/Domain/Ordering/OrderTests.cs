using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Domain.Ordering;

[TestClass]
public class OrderTests
{
    private static OrderLine Line(string skuPrefix = "TS", int quantity = 1) =>
        OrderLine
            .Create(
                SKU.Create(SkuGenerator.Generate(skuPrefix)).Value!,
                quantity,
                Price.Create(10m).Value!
            )
            .Value!;

    [TestMethod]
    public void Place_ShouldRaiseOrderCreatedEvent()
    {
        var customerId = Guid.NewGuid();
        var line = Line();

        var result = Order.Place(customerId, [line]);

        Assert.IsTrue(result.IsSuccess);
        var order = result.Value!;
        var @event = order.UncommittedEvents.OfType<OrderCreated>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(order.Id, @event.OrderId);
        Assert.AreEqual(customerId, @event.CustomerId);
        Assert.AreEqual(1, @event.Lines.Count);
        Assert.AreEqual(OrderStatus.Created, order.Status);
    }

    [TestMethod]
    public void Place_WithNoLines_ShouldReturnFailure()
    {
        var result = Order.Place(Guid.NewGuid(), []);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Place_WithDuplicateSku_ShouldReturnFailure()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var lineA = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var lineB = OrderLine.Create(sku, 3, Price.Create(10m).Value!).Value!;

        var result = Order.Place(Guid.NewGuid(), [lineA, lineB]);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Place_WithEmptyCustomerId_ShouldReturnFailure()
    {
        var result = Order.Place(Guid.Empty, [Line()]);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void MarkLineReserved_SingleLine_ShouldConfirmOrder()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;

        var result = order.MarkLineReserved(line.Sku);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
        Assert.IsTrue(order.UncommittedEvents.OfType<OrderLineReserved>().Any());
        Assert.IsTrue(order.UncommittedEvents.OfType<OrderConfirmed>().Any());
    }

    [TestMethod]
    public void MarkLineReserved_MultipleLines_ShouldConfirmOnlyAfterAllReserved()
    {
        var lineA = Line("AA");
        var lineB = Line("BB");
        var order = Order.Place(Guid.NewGuid(), [lineA, lineB]).Value!;

        order.MarkLineReserved(lineA.Sku);
        Assert.AreEqual(OrderStatus.Created, order.Status);

        order.MarkLineReserved(lineB.Sku);
        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public void MarkLineReserved_CalledTwiceForSameSku_ShouldNotConfirmTwice()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;

        order.MarkLineReserved(line.Sku);
        var secondResult = order.MarkLineReserved(line.Sku);

        Assert.IsTrue(secondResult.IsFailure);
        Assert.AreEqual(1, order.UncommittedEvents.OfType<OrderConfirmed>().Count());
    }

    [TestMethod]
    public void MarkLineReserved_WithUnknownSku_ShouldReturnFailure()
    {
        var order = Order.Place(Guid.NewGuid(), [Line()]).Value!;
        var unknownSku = SKU.Create(SkuGenerator.Generate("ZZ")).Value!;

        var result = order.MarkLineReserved(unknownSku);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Cancel_ShouldRaiseOrderCancelledEvent()
    {
        var order = Order.Place(Guid.NewGuid(), [Line()]).Value!;

        var result = order.Cancel("Insufficient stock");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
        var @event = order.UncommittedEvents.OfType<OrderCancelled>().FirstOrDefault();
        Assert.IsNotNull(@event);
        Assert.AreEqual("Insufficient stock", @event.Reason);
    }

    [TestMethod]
    public void Cancel_WhenAlreadyConfirmed_ShouldReturnFailure()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(line.Sku);

        var result = order.Cancel("Too late");

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Rehydrate_FromHistory_ShouldRestoreState()
    {
        var line = Line();
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(line.Sku);

        var rehydrated = AggregateRoot.Rehydrate<Order>(order.UncommittedEvents.Reverse());

        Assert.IsTrue(rehydrated.IsSuccess);
        Assert.AreEqual(order.Id, rehydrated.Value!.Id);
        Assert.AreEqual(OrderStatus.Confirmed, rehydrated.Value!.Status);
    }
}
