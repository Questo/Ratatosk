using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.UnitTests.Domain.Catalog;

[TestClass]
public class ProductTests
{
    [TestMethod]
    public void Create_WithInvalidInput_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            Product.Create(string.Empty, string.Empty, string.Empty, -1m));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_WithNullOrEmptyName_ShouldThrow(string invalidName)
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value;
        Assert.Throws<ArgumentException>(() =>
            Product.Create(invalidName, sku!.Value, "A description", 10m));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_WithNullOrEmptySku_ShouldThrow(string invalidSku)
    {
        Assert.Throws<ArgumentException>(() =>
            Product.Create("Product Name", invalidSku, "A description", 10m));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_WithNullOrEmptyDescription_ShouldThrow(string invalidDesc)
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value;
        Assert.Throws<ArgumentException>(() =>
            Product.Create("Product Name", sku!.Value, invalidDesc, 10m));
    }

    [TestMethod]
    public void Create_WithInvalidProductName_ShouldThrow()
    {
        // Assuming this name fails internal validation
        var invalidName = new string('!', 300); // too long or invalid
        var ex = Assert.Throws<ArgumentException>(() =>
            Product.Create(invalidName, "SKU123", "Valid description", 10m));
        Assert.Contains("name", ex.Message); // or whatever error your VO returns
    }

    [TestMethod]
    public void Create_WithInvalidSKU_ShouldThrow()
    {
        var invalidSku = "!!!@@@###";
        var ex = Assert.Throws<ArgumentException>(() =>
            Product.Create("Valid Name", invalidSku, "Valid description", 10m));
    }

    [TestMethod]
    public void Create_WithInvalidDescription_ShouldThrow()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value;
        var invalidDesc = new string('x', 1001); // Assume max is 1000
        var ex = Assert.Throws<ArgumentException>(() =>
            Product.Create("Valid Name", sku!.Value, invalidDesc, 10m));
    }

    [TestMethod]
    public void Create_WithInvalidPrice_ShouldThrow()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value;
        var ex = Assert.Throws<ArgumentException>(() =>
            Product.Create("Valid Name", sku!.Value, "Valid description", -10m));
    }

    [TestMethod]
    public void Create_WithValidInput_ShouldReturnProductWithCreatedEvent()
    {
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value;
        var product = Product.Create("Test Product", sku!.Value, "A test product", 99.99m);

        Assert.IsNotNull(product);

        var @event = product.UncommittedEvents
            .OfType<ProductCreated>()
            .FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual("Test Product", @event.Name.Value);
        Assert.AreEqual(sku, @event.Sku);
        Assert.AreEqual("A test product", @event.Description.Value);
        Assert.AreEqual(99.99m, @event.Price.Amount);
    }

    [TestMethod]
    public void CreateSnapshot_ShouldReturnSnapshotWithCorrectValues()
    {
        var product = new ProductBuilder()
            .WithName("Test Product")
            .WithSku(SkuGenerator.Generate("TEST"))
            .WithDescription("Snapshot description")
            .WithPrice(199.99m)
            .Build();

        var snapshot = product.CreateSnapshot() as ProductSnapshot;

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(product.Id, snapshot.AggregateId);
        Assert.AreEqual(typeof(Product).FullName, snapshot.AggregateType);
        Assert.AreEqual(product.Name, snapshot.Name);
        Assert.AreEqual(product.Sku, snapshot.Sku);
        Assert.AreEqual(product.Description, snapshot.Description);
        Assert.AreEqual(product.Price, snapshot.Price);
    }

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

        Assert.IsEmpty(product.UncommittedEvents);
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
