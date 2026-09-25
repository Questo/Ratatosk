using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Infrastructure.Services;

namespace Ratatosk.UnitTests.Infrastructure.Services;

[TestClass]
public class InventoryDomainServiceTests
{
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IInventoryReadModelRepository> _readModelRepoMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private InventoryDomainService _service = null!;

    private Guid _productId;
    private SKU _sku = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _readModelRepoMock = new Mock<IInventoryReadModelRepository>();
        _eventBusMock = new Mock<IEventBus>();
        _service = new InventoryDomainService(
            _repositoryMock.Object,
            _readModelRepoMock.Object,
            _eventBusMock.Object
        );

        _productId = Guid.NewGuid();
        _sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
    }

    private Inventory CreateInventoryWithStock(int available, int reserved = 0)
    {
        var inventory = Inventory.Create(_productId);
        inventory.AddStock(_sku, Quantity.Pieces(available));
        if (reserved > 0)
            inventory.ReserveStock(_sku, reserved);
        inventory.ClearUncommittedEvents();
        return inventory;
    }

    private StockReadModel CreateReadModel(int available = 10, int reserved = 0, string unit = "pcs") =>
        new(_productId, _sku.Value, available, reserved, unit, DateTime.UtcNow);

    [TestMethod]
    public async Task IsProductInStockAsync_ByProductId_WithEnoughStock_ShouldReturnTrue()
    {
        var inventory = CreateInventoryWithStock(available: 10, reserved: 2);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());

        var result = await _service.IsProductInStockAsync(_productId, 5);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsProductInStockAsync_ByProductId_WhenAggregateNotFound_ShouldReturnFalse()
    {
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Failure("not found"));

        var result = await _service.IsProductInStockAsync(_productId, 5);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsProductInStockAsync_ByProductId_WhenReadModelMissing_ShouldReturnFalse()
    {
        var inventory = CreateInventoryWithStock(available: 10);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var result = await _service.IsProductInStockAsync(_productId, 5);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsProductInStockAsync_BySku_ShouldResolveProductIdAndDelegate()
    {
        var inventory = CreateInventoryWithStock(available: 10);
        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(_sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));

        var result = await _service.IsProductInStockAsync(_sku.Value, 5);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsProductInStockAsync_BySku_WhenSkuUnknown_ShouldReturnFalse()
    {
        _readModelRepoMock
            .Setup(r => r.GetBySkuAsync(_sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var result = await _service.IsProductInStockAsync(_sku.Value, 5);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ReserveProductAsync_ByProductId_WithEnoughStock_ShouldSaveAndPublish()
    {
        var inventory = CreateInventoryWithStock(available: 10);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());

        var result = await _service.ReserveProductAsync(_productId, 4);

        Assert.IsTrue(result.IsSuccess);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [TestMethod]
    public async Task ReserveProductAsync_WhenAggregateNotFound_ShouldReturnFailure()
    {
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Failure("not found"));

        var result = await _service.ReserveProductAsync(_productId, 4);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task ReserveProductAsync_WithInsufficientStock_ShouldReturnFailureWithoutThrowing()
    {
        var inventory = CreateInventoryWithStock(available: 2);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());

        var result = await _service.ReserveProductAsync(_productId, 4);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task UnreserveProductAsync_WithReservedStock_ShouldSaveAndPublish()
    {
        var inventory = CreateInventoryWithStock(available: 10, reserved: 5);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel());

        var result = await _service.UnreserveProductAsync(_productId, 3);

        Assert.IsTrue(result.IsSuccess);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task RestockProductAsync_ShouldAddStockUsingReadModelUnitAndSave()
    {
        var inventory = CreateInventoryWithStock(available: 10);
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Success(inventory));
        _readModelRepoMock
            .Setup(r => r.GetByProductIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReadModel(unit: "pcs"));

        var result = await _service.RestockProductAsync(_productId, 7);

        Assert.IsTrue(result.IsSuccess);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [TestMethod]
    public async Task RestockProductAsync_WhenAggregateNotFound_ShouldReturnFailure()
    {
        _repositoryMock
            .Setup(r => r.LoadAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Inventory>.Failure("not found"));

        var result = await _service.RestockProductAsync(_productId, 7);

        Assert.IsTrue(result.IsFailure);
    }
}
