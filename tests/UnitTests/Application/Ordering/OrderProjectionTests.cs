using Moq;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
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
    private Mock<IAggregateRepository<Order>> _orderRepositoryMock = null!;
    private OrderProjection _projection = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IOrderReadModelRepository>();
        _orderRepositoryMock = new Mock<IAggregateRepository<Order>>();
        _projection = new OrderProjection(_repoMock.Object, _orderRepositoryMock.Object);
    }

    [TestMethod]
    public async Task WhenOrderConfirmed_ShouldUpdateStatus()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(sku);

        _orderRepositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        await _projection.WhenAsync(new OrderConfirmed(order.Id), CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<OrderReadModel>(rm => rm.Status == nameof(OrderStatus.Confirmed)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task WhenOrderConfirmed_AndReadModelRowDoesNotExistYet_ShouldStillSaveConfirmedStatus()
    {
        // Regression: OrderCreated's own read-model insert can still be in-flight in a sibling
        // scope when OrderConfirmed arrives (both cascade from the same PlaceOrder event chain),
        // so this handler must not depend on the read-model row already existing.
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 2, Price.Create(10m).Value!).Value!;
        var order = Order.Place(Guid.NewGuid(), [line]).Value!;
        order.MarkLineReserved(sku);

        _orderRepositoryMock
            .Setup(r => r.LoadAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Order>.Success(order));

        await _projection.WhenAsync(new OrderConfirmed(order.Id), CancellationToken.None);

        _repoMock.Verify(
            r =>
                r.SaveAsync(
                    It.Is<OrderReadModel>(rm =>
                        rm.Id == order.Id && rm.Status == nameof(OrderStatus.Confirmed)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
