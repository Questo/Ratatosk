using Microsoft.Extensions.Logging;
using Moq;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class CatalogServiceTests
{
    private Mock<IDispatcher> _dispatcherMock = null!;
    private Mock<ILogger<CatalogService>> _loggerMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private ICatalogService _catalogService = null!;

    [TestInitialize]
    public void Setup()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _loggerMock = new Mock<ILogger<CatalogService>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _catalogService = new CatalogService(_dispatcherMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<IRequest<Result<Guid>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task AddProductAsync_WhenCommandValid_ShouldReturnSuccess()
    {
        // Arrange
        var command = new AddProductCommand("Test Product", SkuGenerator.Generate("TS"), "A test product", 9.99m);
        var expectedResult = Result<Guid>.Success(Guid.NewGuid());

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.AddProductAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedResult.Value, result.Value);
    }

    [TestMethod]
    public async Task AddProductAsync_WhenCommandInvalid_ShouldReturnFailure()
    {
        // Arrange
        var command = new AddProductCommand("", "", "A test product", 9.99m);
        var expectedResult = Result<Guid>.Failure("Invalid command");

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.AddProductAsync(command);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedResult.Error, result.Error);
    }

    [TestMethod]
    public async Task GetProductByIdAsync_WhenQueryValid_ShouldReturnProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductByIdQuery(productId);
        var expectedResult = Result<ProductReadModel>.Success(new ProductReadModel(productId, "Test Product", SkuGenerator.Generate("TS"), "A test product", 9.99m, DateTime.UtcNow));

        _dispatcherMock
            .Setup(x => x.DispatchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.GetProductByIdAsync(query);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedResult.Value, result.Value);
    }

    [TestMethod]
    public async Task GetProductByIdAsync_WhenQueryInvalid_ShouldReturnFailure()
    {
        // Arrange
        var productId = Guid.Empty;
        var query = new GetProductByIdQuery(productId);
        var expectedResult = Result<ProductReadModel>.Failure("Invalid query");

        _dispatcherMock
            .Setup(x => x.DispatchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.GetProductByIdAsync(query);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedResult.Error, result.Error);
    }

    [TestMethod]
    public async Task RemoveProductAsync_WhenCommandValid_ShouldReturnSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new RemoveProductCommand(productId);
        var expectedResult = Result.Success();

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.RemoveProductAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task RemoveProductAsync_WhenCommandInvalid_ShouldReturnFailure()
    {
        // Arrange
        var command = new RemoveProductCommand(Guid.Empty);
        var expectedResult = Result.Failure("Invalid command");

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.RemoveProductAsync(command);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedResult.Error, result.Error);
    }

    [TestMethod]
    public async Task UpdateProductAsync_WhenCommandValid_ShouldReturnSuccess()
    {
        // Arrange
        var command = new UpdateProductCommand(Guid.NewGuid(), "Updated Product", "An updated product", 19.99m);
        var expectedResult = Result.Success();

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.UpdateProductAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task UpdateProductAsync_WhenCommandInvalid_ShouldReturnFailure()
    {
        // Arrange
        var command = new UpdateProductCommand(Guid.Empty, "", null, null);
        var expectedResult = Result.Failure("Invalid command");

        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _catalogService.UpdateProductAsync(command);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedResult.Error, result.Error);
    }

}