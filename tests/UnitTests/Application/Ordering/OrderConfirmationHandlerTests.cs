using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Shared;
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
public class OrderConfirmationHandlerTests
{
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private OrderConfirmationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new OrderConfirmationHandler(
            _repositoryMock.Object,
            _eventBusMock.Object,
            _uowMock.Object
        );
    }

    [TestMethod]
    public async Task WhenStockReservedHasOrderId_ShouldMarkLineReserved()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.ClearUncommittedEvents();

        _repositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        var evt = new StockReserved(Guid.NewGuid(), sku, 2, order.Id);

        await _handler.WhenAsync(evt, CancellationToken.None);

        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public async Task WhenStockReservedHasOrderId_ShouldCommitBeforePublishingEvents()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.ClearUncommittedEvents();

        _repositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        var callOrder = new List<string>();
        _repositoryMock
            .Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.Commit()).Callback(() => callOrder.Add("commit"));
        _eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("publish"))
            .Returns(Task.CompletedTask);

        var evt = new StockReserved(Guid.NewGuid(), sku, 2, order.Id);

        await _handler.WhenAsync(evt, CancellationToken.None);

        // Single-line order: MarkLineReserved raises both OrderLineReserved and OrderConfirmed.
        CollectionAssert.AreEqual(new[] { "save", "commit", "publish", "publish" }, callOrder);
    }

    [TestMethod]
    public async Task WhenStockReservedHasNoOrderId_ShouldDoNothing()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var evt = new StockReserved(Guid.NewGuid(), sku, 2, null);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
