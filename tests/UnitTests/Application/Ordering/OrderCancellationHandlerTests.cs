using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class OrderCancellationHandlerTests
{
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private OrderCancellationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new OrderCancellationHandler(_repositoryMock.Object, _eventBusMock.Object);
    }

    [TestMethod]
    public async Task WhenStockReservationFailed_ShouldCancelOrder()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.ClearUncommittedEvents();

        _repositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        var evt = new StockReservationFailed(Guid.NewGuid(), sku, order.Id, 2, "Not enough stock");

        await _handler.WhenAsync(evt, CancellationToken.None);

        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
    }
}
