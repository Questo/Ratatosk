using Moq;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class RemoveProductCommandHandlerTests
{
    private Mock<IAggregateRepository<Product>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private RemoveProductCommandHandler _handler = null!;

    private static RemoveProductCommand CreateCommand() => new(
        Guid.NewGuid()
    );

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Product>>();
        _eventBusMock = new Mock<IEventBus>();

        _handler = new RemoveProductCommandHandler(
            _repositoryMock.Object,
            _eventBusMock.Object
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Product_Not_Found()
    {
        // Arrange
        var command = CreateCommand();

        _repositoryMock
            .Setup(x => x.LoadAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Product>.Failure("Product not found"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Product not found", result.Error);

        _repositoryMock.Verify(x => x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Success_When_Product_Found()
    {
        // Arrange
        var command = CreateCommand();
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A test product", 9.99m);

        _repositoryMock
            .Setup(x => x.LoadAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Product>.Success(product));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);

        _repositoryMock.Verify(x => x.SaveAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(product.UncommittedEvents.Count));
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Exception_Thrown()
    {
        // Arrange
        var command = CreateCommand();

        _repositoryMock
            .Setup(x => x.LoadAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Database error", result.Error);

        _repositoryMock.Verify(x => x.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
