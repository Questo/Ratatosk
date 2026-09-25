using Moq;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class PlaceOrderCommandHandlerTests
{
    private Mock<IInventoryReadModelRepository> _inventoryRepoMock = null!;
    private Mock<IProductReadModelRepository> _productRepoMock = null!;
    private Mock<IAggregateRepository<Order>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private PlaceOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _inventoryRepoMock = new Mock<IInventoryReadModelRepository>();
        _productRepoMock = new Mock<IProductReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new PlaceOrderCommandHandler(
            _inventoryRepoMock.Object,
            _productRepoMock.Object,
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    [TestMethod]
    public async Task WhenSkuAndProductExist_ShouldPlaceOrderAndReturnOrderId()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow)
            );
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductReadModel(
                    productId,
                    "Widget",
                    sku.Value,
                    "A widget",
                    9.99m,
                    DateTime.UtcNow
                )
            );

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 2)]);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreNotEqual(Guid.Empty, result.Value);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenSkuHasNoInventoryRecord_ShouldReturnFailureWithoutSaving()
    {
        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest("XX-000000", 2)]);

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync("XX-000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task WhenLineQuantityIsZeroOrLess_ShouldReturnFailureWithoutSaving()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync(sku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.UtcNow)
            );
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductReadModel(
                    productId,
                    "Widget",
                    sku.Value,
                    "A widget",
                    9.99m,
                    DateTime.UtcNow
                )
            );

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 0)]);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
