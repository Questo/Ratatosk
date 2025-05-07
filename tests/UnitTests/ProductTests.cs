using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

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

        product.Update(ProductName.Create("New Name").Value!, Description.Create("New Description").Value, Price.Create(120m).Value);

        var @event = product.UncommittedEvents
            .OfType<ProductUpdated>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual("New Name", @event.Name.Value);
        Assert.AreEqual("New Description", @event.Description.Value);
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

        product.Update(ProductName.Create("Same Name").Value!, Description.Create("Same Description").Value, Price.Create(100m).Value);

        Assert.AreEqual(0, product.UncommittedEvents.Count);
    }

    [TestMethod]
    public void Remove_ShouldRaiseProductRemovedEvent()
    {
        var product = new ProductBuilder()
            .WithName("Product Name")
            .WithDescription("Product Description")
            .WithPrice(100m)
            .Build();

        product.Remove();

        var @event = product.UncommittedEvents
            .OfType<ProductRemoved>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(product.Id, @event.ProductId);
    }
}
