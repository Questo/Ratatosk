using Dapper;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresInventoryReadModelRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;
    private IInventoryReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        // Ensure table exists, then truncate
        await _uow.Connection.ExecuteAsync(
            """
                DROP TABLE IF EXISTS inventory_stock_read_models;
                CREATE TABLE IF NOT EXISTS inventory_stock_read_models(
                    product_id uuid PRIMARY KEY,
                    sku text NOT NULL,
                    available integer NOT NULL,
                    reserved integer NOT NULL,
                    unit text NOT NULL,
                    last_updated_utc timestamptz NOT NULL,
                    UNIQUE (sku)
                );
                TRUNCATE TABLE inventory_stock_read_models;
            """,
            transaction: _uow.Transaction
        );

        _repo = new InventoryReadModelRepository(_uow);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_uow == null)
        {
            return;
        }

        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_ShouldInsertNewStockReadModel()
    {
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "NUT-001", 10, 2, "pcs", DateTime.UtcNow);

        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var saved = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.IsNotNull(saved);
        Assert.AreEqual("NUT-001", saved!.Sku);
        Assert.AreEqual(10, saved.Available);
        Assert.AreEqual(2, saved.Reserved);
        Assert.AreEqual("pcs", saved.Unit);
    }

    [TestMethod]
    public async Task SaveAsync_WhenRecordAlreadyExists_ShouldUpsert()
    {
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "NUT-001", 10, 2, "pcs", DateTime.UtcNow);
        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var updated = new StockReadModel(productId, "NUT-001", 25, 5, "pcs", DateTime.UtcNow);
        await _repo.SaveAsync(updated, TestContext.CancellationToken);

        var saved = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.IsNotNull(saved);
        Assert.AreEqual(25, saved!.Available);
        Assert.AreEqual(5, saved.Reserved);
    }

    [TestMethod]
    public async Task GetByProductIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _repo.GetByProductIdAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBySkuAsync_ShouldReturnMatchingRecord()
    {
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "NUT-002", 7, 0, "pcs", DateTime.UtcNow);
        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var result = await _repo.GetBySkuAsync("NUT-002", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(productId, result!.ProductId);
    }

    [TestMethod]
    public async Task GetBySkuAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _repo.GetBySkuAsync("UNKNOWN-SKU", TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ShouldRemoveRecord()
    {
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "NUT-003", 3, 0, "pcs", DateTime.UtcNow);
        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        await _repo.DeleteAsync(productId, TestContext.CancellationToken);

        var result = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    public TestContext TestContext { get; set; }
}
