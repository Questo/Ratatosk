using Moq;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class ReserveStockCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ShouldDelegateToDomainService()
    {
        var domainServiceMock = new Mock<IInventoryDomainService>();
        var productId = Guid.NewGuid();
        domainServiceMock
            .Setup(s => s.ReserveProductAsync(productId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new ReserveStockCommandHandler(domainServiceMock.Object);
        var result = await handler.HandleAsync(new ReserveStockCommand(productId, 3));

        Assert.IsTrue(result.IsSuccess);
        domainServiceMock.Verify(
            s => s.ReserveProductAsync(productId, 3, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task HandleAsync_WhenDomainServiceFails_ShouldReturnFailure()
    {
        var domainServiceMock = new Mock<IInventoryDomainService>();
        var productId = Guid.NewGuid();
        domainServiceMock
            .Setup(s => s.ReserveProductAsync(productId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("not enough stock"));

        var handler = new ReserveStockCommandHandler(domainServiceMock.Object);
        var result = await handler.HandleAsync(new ReserveStockCommand(productId, 3));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("not enough stock", result.Error);
    }
}

[TestClass]
public class UnreserveStockCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ShouldDelegateToDomainService()
    {
        var domainServiceMock = new Mock<IInventoryDomainService>();
        var productId = Guid.NewGuid();
        domainServiceMock
            .Setup(s => s.UnreserveProductAsync(productId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new UnreserveStockCommandHandler(domainServiceMock.Object);
        var result = await handler.HandleAsync(new UnreserveStockCommand(productId, 2));

        Assert.IsTrue(result.IsSuccess);
        domainServiceMock.Verify(
            s => s.UnreserveProductAsync(productId, 2, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}

[TestClass]
public class RestockCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ShouldDelegateToDomainService()
    {
        var domainServiceMock = new Mock<IInventoryDomainService>();
        var productId = Guid.NewGuid();
        domainServiceMock
            .Setup(s => s.RestockProductAsync(productId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new RestockCommandHandler(domainServiceMock.Object);
        var result = await handler.HandleAsync(new RestockCommand(productId, 20));

        Assert.IsTrue(result.IsSuccess);
        domainServiceMock.Verify(
            s => s.RestockProductAsync(productId, 20, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
