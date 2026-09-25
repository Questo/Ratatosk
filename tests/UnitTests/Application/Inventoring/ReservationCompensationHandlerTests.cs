using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
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
    private ReservationCompensationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new ReservationCompensationHandler(
            _readModelRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    private static OrderLine Line(SKU sku, int quantity) =>
        OrderLine.Create(sku, quantity, Price.Create(10m).Value!).Value!;

    [TestMethod]
    public async Task WhenLineWasReserved_ShouldReleaseAndPublishStockReleased()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();
        var inventory = Inventory.Create(productId);
        inventory.AddStock(sku, Quantity.Pieces(10));
        inventory.ReserveStock(sku, 3);
        inventory.ClearUncommittedEvents();

        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReadModel(productId, sku.Value, 7, 3, "pcs", DateTime.UtcNow));
        _repositoryMock
            .Setup(r => r.LoadAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var evt = new OrderCancelled(Guid.NewGuid(), "Insufficient stock", [Line(sku, 3)]);

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
}
