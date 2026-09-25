using Microsoft.Extensions.Logging;
using Moq;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Commands;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
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
    private Mock<IOrderReadModelRepository> _orderReadModelRepoMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<ILogger<PlaceOrderCommandHandler>> _loggerMock = null!;
    private PlaceOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _inventoryRepoMock = new Mock<IInventoryReadModelRepository>();
        _productRepoMock = new Mock<IProductReadModelRepository>();
        _repositoryMock = new Mock<IAggregateRepository<Order>>();
        _orderReadModelRepoMock = new Mock<IOrderReadModelRepository>();
        _eventBusMock = new Mock<IEventBus>();
        _uowMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<PlaceOrderCommandHandler>>();
        _handler = new PlaceOrderCommandHandler(
            _inventoryRepoMock.Object,
            _productRepoMock.Object,
            _repositoryMock.Object,
            _orderReadModelRepoMock.Object,
            _eventBusMock.Object,
            _uowMock.Object,
            _loggerMock.Object
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
    public async Task WhenOrderIsSaved_ShouldSaveReadModelAndCommitBeforePublishingEvents()
    {
        // Must be committed before publishing, or a handler further down the cascade races it.
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

        var callOrder = new List<string>();
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .Returns(Task.CompletedTask);
        _orderReadModelRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<OrderReadModel>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save-read-model"))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.Commit()).Callback(() => callOrder.Add("commit"));
        _eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("publish"))
            .Returns(Task.CompletedTask);

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 2)]);

        await _handler.HandleAsync(command, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "save", "save-read-model", "commit", "publish" },
            callOrder
        );
    }

    [TestMethod]
    public async Task WhenOrderIsSaved_ShouldSaveReadModelWithCreatedStatus()
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

        _orderReadModelRepoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<OrderReadModel>(rm =>
                        rm.Id == result.Value
                        && rm.Status == nameof(OrderStatus.Created)
                        && rm.Lines.Count == 1
                    ),
                    It.IsAny<CancellationToken>()
                ),
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

    [TestMethod]
    public async Task WhenSecondLineIsInvalid_ShouldFailWithoutSavingAnything()
    {
        var validSku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var productId = Guid.NewGuid();

        _inventoryRepoMock
            .Setup(r => r.GetBySkuAsync(validSku.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StockReadModel(productId, validSku.Value, 10, 0, "pcs", DateTime.UtcNow)
            );
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductReadModel(
                    productId,
                    "Widget",
                    validSku.Value,
                    "A widget",
                    9.99m,
                    DateTime.UtcNow
                )
            );

        var command = new PlaceOrderCommand(
            Guid.NewGuid(),
            [new OrderLineRequest(validSku.Value, 2), new OrderLineRequest("XX-000000", 1)]
        );

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        _repositoryMock.Verify(
            r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _orderReadModelRepoMock.Verify(
            r => r.SaveAsync(It.IsAny<OrderReadModel>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task WhenPublishingEventsThrows_ShouldStillReturnSuccessSinceOrderWasCommitted()
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
        _eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("downstream handler exploded"));

        var command = new PlaceOrderCommand(Guid.NewGuid(), [new OrderLineRequest(sku.Value, 2)]);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreNotEqual(Guid.Empty, result.Value);
    }
}
