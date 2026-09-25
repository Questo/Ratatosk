using Microsoft.Extensions.Logging;
using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Inventoring.Queries;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class InventoryServiceTests
{
    private Mock<IDispatcher> _dispatcherMock = null!;
    private Mock<ILogger<InventoryService>> _loggerMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private IInventoryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _loggerMock = new Mock<ILogger<InventoryService>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new InventoryService(_dispatcherMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
    }

    [TestMethod]
    public async Task ReserveStockAsync_WhenSuccessful_ShouldCommitAndReturnSuccess()
    {
        var command = new ReserveStockCommand(Guid.NewGuid(), 3);
        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.ReserveStockAsync(command);

        Assert.IsTrue(result.IsSuccess);
        _unitOfWorkMock.Verify(u => u.Commit(), Times.Once);
    }

    [TestMethod]
    public async Task ReserveStockAsync_WhenFailed_ShouldNotCommit()
    {
        var command = new ReserveStockCommand(Guid.NewGuid(), 3);
        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("nope"));

        var result = await _service.ReserveStockAsync(command);

        Assert.IsTrue(result.IsFailure);
        _unitOfWorkMock.Verify(u => u.Commit(), Times.Never);
    }

    [TestMethod]
    public async Task UnreserveStockAsync_WhenSuccessful_ShouldCommitAndReturnSuccess()
    {
        var command = new UnreserveStockCommand(Guid.NewGuid(), 2);
        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.UnreserveStockAsync(command);

        Assert.IsTrue(result.IsSuccess);
        _unitOfWorkMock.Verify(u => u.Commit(), Times.Once);
    }

    [TestMethod]
    public async Task RestockAsync_WhenSuccessful_ShouldCommitAndReturnSuccess()
    {
        var command = new RestockCommand(Guid.NewGuid(), 20);
        _dispatcherMock
            .Setup(x => x.DispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.RestockAsync(command);

        Assert.IsTrue(result.IsSuccess);
        _unitOfWorkMock.Verify(u => u.Commit(), Times.Once);
    }

    [TestMethod]
    public async Task GetStockByProductIdAsync_ShouldReturnDispatcherResult()
    {
        var query = new GetStockByProductIdQuery(Guid.NewGuid());
        var expected = Result<StockReadModel>.Success(
            new StockReadModel(query.ProductId, "TS-1234", 5, 0, "pcs", DateTime.UtcNow)
        );
        _dispatcherMock
            .Setup(x => x.DispatchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetStockByProductIdAsync(query);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expected.Value, result.Value);
    }

    [TestMethod]
    public async Task GetStockBySkuAsync_ShouldReturnDispatcherResult()
    {
        var query = new GetStockBySkuQuery("TS-1234");
        var expected = Result<StockReadModel>.Success(
            new StockReadModel(Guid.NewGuid(), "TS-1234", 5, 0, "pcs", DateTime.UtcNow)
        );
        _dispatcherMock
            .Setup(x => x.DispatchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetStockBySkuAsync(query);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expected.Value, result.Value);
    }
}
