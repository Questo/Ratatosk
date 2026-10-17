using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Inventoring;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class CatalogCommandTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task AddProduct_ProjectsTheProduct_AndProvisionsAnEmptyInventory()
    {
        var sku = SkuGenerator.Generate("TS");

        var result = await _host.SendAsync(new AddProductCommand("Test Product", sku, "A product for testing", 9.99m));

        Assert.IsTrue(result.IsSuccess);
        var product = await _host.Products.GetByIdAsync(result.Value);
        Assert.AreEqual(sku, product!.Sku);
        Assert.AreEqual(9.99m, product.Price);

        var stock = await _host.Stock.GetBySkuAsync(sku);
        Assert.AreEqual(result.Value, stock!.ProductId);
        Assert.AreEqual(0, stock.Available);
        Assert.AreEqual(1, (await _host.LoadAsync<Inventory>(result.Value)).Version);
    }

    [TestMethod]
    public async Task AddProduct_WithASkuAlreadyInUse_FailsAndStoresNothing()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 0);

        var result = await _host.SendAsync(new AddProductCommand("Other Product", sku, "Another product here", 1m));

        Assert.IsTrue(result.IsFailure);
        Assert.HasCount(1, (await _host.Products.GetAllAsync()).Items);
    }

    [TestMethod]
    public async Task AddProduct_WithInvalidInput_Fails()
    {
        var result = await _host.SendAsync(new AddProductCommand("", "not a sku", "too short", -1m));

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task UpdateProduct_WithOnlyANewName_KeepsDescriptionAndPrice()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 0, price: 12m);

        var result = await _host.SendAsync(new UpdateProductCommand(productId, "Renamed Product", null, null));

        Assert.IsTrue(result.IsSuccess);
        var product = await _host.LoadAsync<Product>(productId);
        Assert.AreEqual("Renamed Product", product.Name.Value);
        Assert.AreEqual("A product for testing", product.Description.Value);
        Assert.AreEqual(12m, product.Price.Amount);
        Assert.AreEqual("Renamed Product", (await _host.Products.GetByIdAsync(productId))!.Name);
    }

    [TestMethod]
    public async Task UpdateProduct_WithAnInvalidDescription_FailsAndChangesNothing()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 0);
        var versionBefore = (await _host.LoadAsync<Product>(productId)).Version;

        var result = await _host.SendAsync(new UpdateProductCommand(productId, "Renamed Product", "short", null));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(versionBefore, (await _host.LoadAsync<Product>(productId)).Version);
    }

    [TestMethod]
    public async Task UpdateProduct_WithAnInvalidPrice_FailsAndChangesNothing()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 0);
        var versionBefore = (await _host.LoadAsync<Product>(productId)).Version;

        var result = await _host.SendAsync(new UpdateProductCommand(productId, "Renamed Product", null, -1m));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(versionBefore, (await _host.LoadAsync<Product>(productId)).Version);
    }

    [TestMethod]
    public async Task UpdateProduct_UnknownProduct_Fails()
    {
        var result = await _host.SendAsync(new UpdateProductCommand(Guid.NewGuid(), "Renamed Product", null, null));

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task RemoveProduct_RemovesItFromTheCatalog_ButKeepsItsStockRecord()
    {
        var (productId, sku) = await _host.AddStockedProductAsync(onHand: 4);

        var result = await _host.SendAsync(new RemoveProductCommand(productId));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(await _host.Products.GetByIdAsync(productId));
        Assert.AreEqual(4, (await _host.Stock.GetBySkuAsync(sku))!.Available);
    }
}
