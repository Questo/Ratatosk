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
public class ReservationCompensationHandlerTests
{
    private Mock<IInventoryReadModelRepository> _readModelRepoMock = null!;
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private ReservationCompensationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new ReservationCompensationHandler(
            _readModelRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object,
            _uowMock.Object
        );
    }

    private static OrderLine Line(SKU sku, int quantity) =>
        OrderLine.Create(sku, quantity, Price.Create(10m).Value!).Value!;

    [TestMethod]
    public async Task WhenLineWasReserved_ShouldReleaseAndPublishStockReleased()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3, orderId);
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 7, 3, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(orderId, "Insufficient stock", [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b =>
                b.PublishAsync(
                    It.Is<DomainEvent>(e => e.GetType() == typeof(StockReleased)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenLineWasReserved_ShouldCommitBeforePublishingEvents()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3, orderId);
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 7, 3, "pcs", DateTime.UtcNow));
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

        var evt = new OrderCancelled(orderId, "Insufficient stock", [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "save", "commit", "publish" }, callOrder);
    }

    [TestMethod]
    public async Task WhenLineWasNeverReserved_ShouldNotThrow()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(Guid.NewGuid(), "Insufficient stock", [Line(sku, 3)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task WhenAnotherOrderHoldsAReservationForTheSameSku_ShouldNotReleaseIt()
    {
        // Regression: releasing must be scoped to the cancelled order's own reservation, not
        // the SKU's total Reserved count, or it would silently steal another order's stock.
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var cancelledOrderId = Guid.NewGuid();
        var otherOrderId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 5, cancelledOrderId);
        inventory.ReserveStock(sku, 3, otherOrderId);
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 10, 8, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(cancelledOrderId, "Insufficient stock", [Line(sku, 5)]);

        await _handler.WhenAsync(evt, CancellationToken.None);

        var released = inventory.UncommittedEvents.OfType<StockReleased>().Single();
        Assert.AreEqual(5, released.Quantity);
        Assert.IsFalse(inventory.IsInStock(sku, 8));
        Assert.IsTrue(inventory.IsInStock(sku, 7));
    }
}
