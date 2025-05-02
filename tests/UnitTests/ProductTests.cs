using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.UnitTests;

[TestClass]
public class ProductTests
{
    [TestMethod]
    public void Update_WithChangedFields_ShouldRaiseProductUpdatedEvent()
    {
        var product = new ProductBuilder()
            .WithName("Old Name")
            .WithDescription("Old Description")
            .WithPrice(100m)
            .Build();

        product.Update("New Name", "New Description", Price.Create(120m).Value);

        var @event = product.UncommittedEvents
            .OfType<ProductUpdated>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual("New Name", @event.Name);
        Assert.AreEqual("New Description", @event.Description);
        Assert.AreEqual(120m, @event.Price.Amount);
    }

    [TestMethod]
    public void Update_WithNoChanges_ShouldNotRaiseEvent()
    {
        var product = new ProductBuilder()
            .WithName("Same Name")
            .WithDescription("Same Description")
            .WithPrice(100m)
            .Build();

        product.Update("Same Name", "Same Description", Price.Create(100m).Value);

        Assert.AreEqual(0, product.UncommittedEvents.Count);
    }
}
