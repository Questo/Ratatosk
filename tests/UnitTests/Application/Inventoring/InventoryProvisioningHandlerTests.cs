using Moq;
using Ratatosk.Application.Inventoring;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class InventoryProvisioningHandlerTests
{
    private Mock<IAggregateRepository<Inventory>> _repositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private InventoryProvisioningHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAggregateRepository<Inventory>>();
        _eventBusMock = new Mock<IEventBus>();
        _handler = new InventoryProvisioningHandler(_repositoryMock.Object, _eventBusMock.Object);
    }

    [TestMethod]
    public async Task When_ProductCreated_ShouldCreateAndSaveInventoryWithProductIdAsAggregateId()
    {
        var evt = new ProductCreated(
            Guid.NewGuid(),
            ProductName.Create("Product").Value!,
            SKU.Create(SkuGenerator.Generate("FOO")).Value!,
            Description.Create("Description").Value!,
            Price.Create(10.99m).Value!
        );

        Inventory? saved = null;
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<Inventory>(), It.IsAny<CancellationToken>()))
            .Callback<Inventory, CancellationToken>((i, _) => saved = i);

        await _handler.WhenAsync(evt, CancellationToken.None);

        Assert.IsNotNull(saved);
        Assert.AreEqual(evt.ProductId, saved!.Id);

        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }
}
