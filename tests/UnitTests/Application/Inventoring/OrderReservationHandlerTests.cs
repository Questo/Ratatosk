using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class OrderReservationHandlerTests
{
    private Mock<IInventoryReadModelRepository> _readModelRepoMock = null!;
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private OrderReservationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new OrderReservationHandler(
            _readModelRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object,
            _uowMock.Object
        );
    }

    private static OrderLine Line(SKU sku, int quantity) =>
        OrderLine.Create(sku, quantity, Price.Create(10m).Value!).Value!;

    [TestMethod]
    public async Task WhenStockAvailable_ShouldReserveAndPublishStockReserved()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e =>
                        e.GetType() == typeof(StockReserved)
                        && ((StockReserved)e).OrderId == orderId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenStockAvailable_ShouldCommitBeforePublishingEvents()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var callOrder = new List<string>();
        _repositoryMock
            .Setup(r => r.SaveAsync(inventory, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.Commit()).Callback(() => callOrder.Add("commit"));
        _eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("publish"))
            .Returns(Task.CompletedTask);

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "save", "commit", "publish" }, callOrder);
    }

    [TestMethod]
    public async Task WhenSkuHasNoInventoryRecord_ShouldPublishStockReservationFailed()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var orderId = Guid.NewGuid();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e =>
                        e.GetType() == typeof(StockReservationFailed)
                        && ((StockReservationFailed)e).OrderId == orderId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task WhenFirstLineFails_ShouldNotAttemptLaterLines()
    {
        var skuA = SKU.Create(SkuGenerator.Generate("AA")).Value!;
        var skuB = SKU.Create(SkuGenerator.Generate("BB")).Value!;
        var orderId = Guid.NewGuid();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(skuA.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(skuA, 3), Line(skuB, 2)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _readModelRepoMock.Verify(
            r => r.GetBySkuAsync(skuB.Value, It.IsAny<CancellationToken>()),
            Times.Never
        );
        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenInventoryHasNoStockEntryForSku_ShouldPublishStockReservationFailed()
    {
        // Regression: a product with no AddStock ever called has no entry in the aggregate at all.
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 0, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCreated(orderId, Guid.NewGuid(), [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e =>
                        e.GetType() == typeof(StockReservationFailed)
                        && ((StockReservationFailed)e).OrderId == orderId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
