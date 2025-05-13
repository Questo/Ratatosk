using Moq;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class AddProductCommandHandlerTests
{
    private Mock<IAggregateRepository<Product>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IProductDomainService> _domainServiceMock = null!;
    private AddProductCommandHandler _handler = null!;

    private AddProductCommand CreateCommand() => new(
        Name: "Test Product",
        Sku: SkuGenerator.Generate("TS"),
        Description: "A test product",
        Price: 9.99m
    );

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Product>>();
        _eventBusMock = new Mock<IEventBus>();
        _domainServiceMock = new Mock<IProductDomainService>();

        _handler = new AddProductCommandHandler(
            _repositoryMock.Object,
            _eventBusMock.Object,
            _domainServiceMock.Object
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Success_When_Sku_Is_Unique()
    {
        // Arrange
        var command = CreateCommand();

        _domainServiceMock
            .Setup(x => x.IsSkuUniqueAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Product? savedProduct = null;

        _repositoryMock
            .Setup(x => x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => savedProduct = p);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreNotEqual(Guid.Empty, result.Value);

        Assert.IsNotNull(savedProduct);
        Assert.AreEqual(command.Sku, savedProduct!.Sku.Value);
        Assert.AreEqual(command.Name, savedProduct.Name.Value);
        Assert.AreEqual(command.Description, savedProduct.Description.Value);
        Assert.AreEqual(command.Price, savedProduct.Price.Amount);

        _repositoryMock.Verify(x =>
            x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);

        _eventBusMock.Verify(x =>
            x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Sku_Is_Not_Unique()
    {
        // Arrange
        var command = CreateCommand();

        _domainServiceMock
            .Setup(x => x.IsSkuUniqueAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"SKU {command.Sku} is already in use", result.Error);

        _repositoryMock.Verify(x => x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Exception_Thrown()
    {
        // Arrange
        var command = CreateCommand();

        _domainServiceMock
            .Setup(x => x.IsSkuUniqueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("boom", result.Error);

        _repositoryMock.Verify(x => x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
