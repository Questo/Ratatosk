using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class InventoryProjectionTests
{
    private Mock<IInventoryReadModelRepository> _repoMock = null!;
    private InventoryProjection _projection = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IInventoryReadModelRepository>();
        _projection = new InventoryProjection(_repoMock.Object);
    }

    [TestMethod]
    public async Task When_ProductCreated_ShouldSaveEmptyStockReadModel()
    {
        var evt = new ProductCreated(
            Guid.NewGuid(),
            ProductName.Create("Product").Value!,
            SKU.Create(SkuGenerator.Generate("FOO")).Value!,
            Description.Create("Description").Value!,
            Price.Create(10.99m).Value!
        );

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            repo =>
                repo.SaveAsync(
                    It.Is<StockReadModel>(rm =>
                        rm.ProductId == evt.ProductId
                        && rm.Sku == evt.Sku.Value
                        && rm.Available == 0
                        && rm.Reserved == 0
                        && rm.Unit == "pcs"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task When_ProductRemoved_ShouldDeleteReadModel()
    {
        var productId = Guid.NewGuid();
        var evt = new ProductRemoved(productId);

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r => r.DeleteAsync(productId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task When_StockAdded_AndModelExists_ShouldIncreaseAvailable()
    {
        var productId = Guid.NewGuid();
        var sku = SKU.Create(SkuGenerator.Generate("FOO")).Value!;
        var existing = new StockReadModel(productId, sku.Value, 5, 0, "pcs", DateTime.MinValue);

        _repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var evt = new StockAdded(productId, sku, Quantity.Pieces(10));

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<StockReadModel>(rm => rm.Available == 15),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task When_StockAdded_AndModelDoesNotExist_ShouldDoNothing()
    {
        var productId = Guid.NewGuid();
        var sku = SKU.Create(SkuGenerator.Generate("FOO")).Value!;

        _repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var evt = new StockAdded(productId, sku, Quantity.Pieces(10));

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r => r.SaveAsync(It.IsAny<StockReadModel>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task When_StockReserved_ShouldIncreaseReserved()
    {
        var productId = Guid.NewGuid();
        var sku = SKU.Create(SkuGenerator.Generate("FOO")).Value!;
        var existing = new StockReadModel(productId, sku.Value, 10, 2, "pcs", DateTime.MinValue);

        _repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var evt = new StockReserved(productId, sku, 3);

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<StockReadModel>(rm => rm.Reserved == 5),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task When_StockReleased_ShouldDecreaseReserved()
    {
        var productId = Guid.NewGuid();
        var sku = SKU.Create(SkuGenerator.Generate("FOO")).Value!;
        var existing = new StockReadModel(productId, sku.Value, 10, 5, "pcs", DateTime.MinValue);

        _repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var evt = new StockReleased(productId, sku, 3);

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<StockReadModel>(rm => rm.Reserved == 2),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task When_StockRemoved_ShouldDecreaseAvailable()
    {
        var productId = Guid.NewGuid();
        var sku = SKU.Create(SkuGenerator.Generate("FOO")).Value!;
        var existing = new StockReadModel(productId, sku.Value, 10, 0, "pcs", DateTime.MinValue);

        _repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var evt = new StockRemoved(productId, sku, Quantity.Pieces(4));

        await _projection.WhenAsync(evt, CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<StockReadModel>(rm => rm.Available == 6),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
