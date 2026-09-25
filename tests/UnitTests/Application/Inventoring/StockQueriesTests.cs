using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Inventoring.Queries;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class GetStockByProductIdQueryHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_WhenFound_ShouldReturnSuccess()
    {
        var repoMock = new Mock<IInventoryReadModelRepository>();
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "TS-1234", 5, 1, "pcs", DateTime.UtcNow);

        repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        var handler = new GetStockByProductIdQueryHandler(repoMock.Object);
        var result = await handler.HandleAsync(new GetStockByProductIdQuery(productId));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(stock, result.Value);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNotFound_ShouldReturnFailure()
    {
        var repoMock = new Mock<IInventoryReadModelRepository>();
        var productId = Guid.NewGuid();

        repoMock
            .Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var handler = new GetStockByProductIdQueryHandler(repoMock.Object);
        var result = await handler.HandleAsync(new GetStockByProductIdQuery(productId));

        Assert.IsTrue(result.IsFailure);
    }
}

[TestClass]
public class GetStockBySkuQueryHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_WhenFound_ShouldReturnSuccess()
    {
        var repoMock = new Mock<IInventoryReadModelRepository>();
        var stock = new StockReadModel(Guid.NewGuid(), "TS-1234", 5, 1, "pcs", DateTime.UtcNow);

        repoMock
            .Setup(r => r.GetBySkuAsync("TS-1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        var handler = new GetStockBySkuQueryHandler(repoMock.Object);
        var result = await handler.HandleAsync(new GetStockBySkuQuery("TS-1234"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(stock, result.Value);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNotFound_ShouldReturnFailure()
    {
        var repoMock = new Mock<IInventoryReadModelRepository>();

        repoMock
            .Setup(r => r.GetBySkuAsync("TS-1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReadModel?)null);

        var handler = new GetStockBySkuQueryHandler(repoMock.Object);
        var result = await handler.HandleAsync(new GetStockBySkuQuery("TS-1234"));

        Assert.IsTrue(result.IsFailure);
    }
}
