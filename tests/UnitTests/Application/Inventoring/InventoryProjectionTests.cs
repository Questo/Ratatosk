using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class InventoryProjectionTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task AddingAProduct_ProjectsAnEmptyStockRecord()
    {
        var (productId, sku) = await _host.AddStockedProductAsync(onHand: 0);

        var stock = await _host.Stock.GetByProductIdAsync(productId);
        Assert.AreEqual(sku, stock!.Sku);
        Assert.AreEqual(0, stock.OnHand);
        Assert.AreEqual(0, stock.Reserved);
        Assert.AreEqual("pcs", stock.Unit);
    }

    [TestMethod]
    public async Task AStockEventDeliveredLateOrTwice_LeavesTheRecordMatchingTheInventory()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 10);
        await _host.SendAsync(new ReserveStockCommand(productId, 3));
        var restocked = (await _host.StreamAsync<Inventory>(productId)).OfType<StockAdded>().Single();

        await _host.GetService<IEventBus>().PublishAsync(restocked);

        var stock = await _host.Stock.GetByProductIdAsync(productId);
        Assert.AreEqual(10, stock!.OnHand);
        Assert.AreEqual(3, stock.Reserved);
        Assert.AreEqual(7, stock.Available);
    }

    [TestMethod]
    public async Task ProductCreatedDeliveredAgain_DoesNotResetTheStockRecord()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 10);
        var created = (await _host.StreamAsync<Product>(productId)).OfType<ProductCreated>().Single();

        await _host.GetService<IEventBus>().PublishAsync(created);

        Assert.AreEqual(10, (await _host.Stock.GetByProductIdAsync(productId))!.OnHand);
    }
}
