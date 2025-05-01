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

        var result = product.Update("New Name", "New Description", 120m);

        Assert.IsTrue(result.IsSuccess);
        var @event = product.UncommittedEvents
            .OfType<ProductUpdated>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual("New Name", @event.Name);
        Assert.AreEqual("New Description", @event.Description);
        Assert.AreEqual(120m, @event.Price);
    }

    [TestMethod]
    public void Update_WithNoChanges_ShouldReturnFailure()
    {
        var product = new ProductBuilder()
            .WithName("Same Name")
            .WithDescription("Same Description")
            .WithPrice(100m)
            .Build();

        var result = product.Update("Same Name", "Same Description", 100m);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Nothing has changed", result.Error);
        Assert.AreEqual(0, product.UncommittedEvents.Count);
    }
}
