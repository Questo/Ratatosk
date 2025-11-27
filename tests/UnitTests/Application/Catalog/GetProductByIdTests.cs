using Moq;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class GetProductByIdQueryHandlerTests
{
    private Mock<IProductReadModelRepository> _readModelRepositoryMock = null!;
    private GetProductByIdQueryHandler _handler = null!;

    private static GetProductByIdQuery CreateQuery() => new(Guid.NewGuid());

    [TestInitialize]
    public void Setup()
    {
        _readModelRepositoryMock = new Mock<IProductReadModelRepository>();
        _handler = new(_readModelRepositoryMock.Object);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Product_Not_Found()
    {
        // Arrange
        var query = CreateQuery();

        _readModelRepositoryMock
            .Setup(x => x.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReadModel?)null);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Product not found", result.Error);

        _readModelRepositoryMock.Verify(
            x => x.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Success_When_Product_Found()
    {
        // Arrange
        var query = CreateQuery();
        var productReadModel = new ProductReadModel(
            query.Id,
            "Test Product",
            SkuGenerator.Generate("TS"),
            "A test product",
            9.99m,
            DateTime.UtcNow
        );

        _readModelRepositoryMock
            .Setup(x => x.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productReadModel);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(productReadModel, result.Value);

        _readModelRepositoryMock.Verify(
            x => x.GetByIdAsync(query.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
