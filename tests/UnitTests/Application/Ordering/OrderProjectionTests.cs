using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.UnitTests.Application.Ordering;

[TestClass]
public class OrderProjectionTests
{
    private Mock<IOrderReadModelRepository> _repoMock = null!;
    private OrderProjection _projection = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IOrderReadModelRepository>();
        _projection = new OrderProjection(_repoMock.Object);
    }

    [TestMethod]
    public async Task WhenOrderCreated_ShouldSaveReadModelWithCreatedStatus()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var evt = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), [line]);

        OrderReadModel? saved = null;
        _repoMock
            .Setup(r => r.SaveAsync(It.IsAny<OrderReadModel>(), It.IsAny<CancellationToken>()))
            .Callback<OrderReadModel, CancellationToken>((rm, _) => saved = rm);

        await _projection.WhenAsync(evt, CancellationToken.None);

        Assert.IsNotNull(saved);
        Assert.AreEqual(evt.OrderId, saved!.Id);
        Assert.AreEqual(nameof(OrderStatus.Created), saved.Status);
        Assert.AreEqual(1, saved.Lines.Count);
    }

    [TestMethod]
    public async Task WhenOrderConfirmed_ShouldUpdateStatus()
    {
        var orderId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderReadModel { Id = orderId, Status = nameof(OrderStatus.Created) });

        await _projection.WhenAsync(new OrderConfirmed(orderId), CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<OrderReadModel>(rm => rm.Status == nameof(OrderStatus.Confirmed)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
