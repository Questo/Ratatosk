using Moq;
using Ratatosk.Application.Catalog.Projections;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class ProductProjectionTests
{
    private Mock<IProductReadModelRepository> _repoMock = null!;
    private ProductProjection _projection = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IProductReadModelRepository>();
        _projection = new ProductProjection(_repoMock.Object);
    }

    [TestMethod]
    public async Task When_ProductCreated_ShouldSaveReadModel()
    {
        // Arrange
        var evt = new ProductCreated(
            Guid.NewGuid(),
            ProductName.Create("Product").Value!,
            SKU.Create(SkuGenerator.Generate("FOO")).Value!,
            Description.Create("Description").Value!,
            Price.Create(10.99m).Value!
        );

        // Act
        await _projection.WhenAsync(evt, CancellationToken.None);

        // Assert
        _repoMock.Verify(repo => repo.SaveAsync(
            It.Is<ProductReadModel>(rm =>
                rm.Id == evt.ProductId &&
                rm.Name == evt.Name.Value &&
                rm.Sku == evt.Sku.Value &&
                rm.Description == evt.Description.Value &&
                rm.Price == evt.Price.Amount &&
                rm.LastUpdatedUtc == evt.OccurredAtUtc.UtcDateTime),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task When_ProductUpdated_AndModelExists_ShouldUpdateAndSave()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existing = new ProductReadModel
        {
            Id = productId,
            Name = "Old Name",
            Description = "Old Desc",
            Price = 5.00m,
            Sku = SkuGenerator.Generate("FOO"),
            LastUpdatedUtc = DateTime.MinValue
        };

        _repoMock.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var evt = new ProductUpdated(
            productId,
            ProductName.Create("New Name").Value!,
            Description.Create("New Description").Value!,
            Price.Create(15.50m).Value!
        );

        // Act
        await _projection.WhenAsync(evt, CancellationToken.None);

        // Assert
        _repoMock.Verify(repo => repo.SaveAsync(
            It.Is<ProductReadModel>(rm =>
                rm.Name == evt.Name.Value &&
                rm.Description == evt.Description.Value &&
                rm.Price == evt.Price.Amount &&
                rm.LastUpdatedUtc == evt.OccurredAtUtc.UtcDateTime),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task When_ProductUpdated_AndModelDoesNotExist_ShouldDoNothing()
    {
        // Arrange
        var evt = new ProductUpdated(
            Guid.NewGuid(),
            ProductName.Create("New Name").Value!,
            Description.Create("New Desc").Value!,
            Price.Create(15.50m).Value!
        );

        _repoMock.Setup(r => r.GetByIdAsync(evt.ProductId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((ProductReadModel?)null);

        // Act
        await _projection.WhenAsync(evt, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<ProductReadModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task When_ProductRemoved_ShouldDeleteReadModel()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var evt = new ProductRemoved(productId);

        // Act
        await _projection.WhenAsync(evt, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.DeleteAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
