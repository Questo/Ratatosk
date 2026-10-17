using Dapper;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
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
                    on_hand integer NOT NULL,
                    reserved integer NOT NULL,
                    unit text NOT NULL,
                    version integer NOT NULL DEFAULT 0,
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
        var stock = new StockReadModel(productId, "NUT-001", 10, 2, "pcs", 2, DateTime.UtcNow);

        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var saved = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.IsNotNull(saved);
        Assert.AreEqual("NUT-001", saved!.Sku);
        Assert.AreEqual(10, saved.OnHand);
        Assert.AreEqual(2, saved.Reserved);
        Assert.AreEqual(8, saved.Available);
        Assert.AreEqual(2, saved.Version);
        Assert.AreEqual("pcs", saved.Unit);
    }

    [TestMethod]
    public async Task SaveAsync_WhenANewerVersionExists_ShouldKeepIt()
    {
        var productId = Guid.NewGuid();
        var newer = new StockReadModel(productId, "NUT-001", 25, 5, "pcs", 3, DateTime.UtcNow);
        await _repo.SaveAsync(newer, TestContext.CancellationToken);

        var older = new StockReadModel(productId, "NUT-001", 10, 2, "pcs", 2, DateTime.UtcNow);
        await _repo.SaveAsync(older, TestContext.CancellationToken);

        var saved = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.AreEqual(25, saved!.OnHand);
        Assert.AreEqual(3, saved.Version);
    }

    [TestMethod]
    public async Task SaveAsync_WhenRecordAlreadyExists_ShouldUpsert()
    {
        var productId = Guid.NewGuid();
        var stock = new StockReadModel(productId, "NUT-001", 10, 2, "pcs", 2, DateTime.UtcNow);
        await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var updated = new StockReadModel(productId, "NUT-001", 25, 5, "pcs", 3, DateTime.UtcNow);
        await _repo.SaveAsync(updated, TestContext.CancellationToken);

        var saved = await _repo.GetByProductIdAsync(productId, TestContext.CancellationToken);
        Assert.IsNotNull(saved);
        Assert.AreEqual(25, saved!.OnHand);
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
        var stock = new StockReadModel(productId, "NUT-002", 7, 0, "pcs", 1, DateTime.UtcNow);
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
    public async Task GetPageAsync_ReturnsEveryRecordOrderedBySku()
    {
        var b = new StockReadModel(Guid.NewGuid(), "NUT-B", 10, 4, "pcs", 1, DateTime.UtcNow);
        var c = new StockReadModel(Guid.NewGuid(), "NUT-C", 3, 0, "pcs", 1, DateTime.UtcNow);
        var a = new StockReadModel(Guid.NewGuid(), "NUT-A", 7, 2, "pcs", 1, DateTime.UtcNow);
        foreach (var stock in new[] { b, c, a })
            await _repo.SaveAsync(stock, TestContext.CancellationToken);

        var result = await _repo.GetPageAsync(cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "NUT-A", "NUT-B", "NUT-C" },
            result.Items.Select(s => s.Sku).ToArray()
        );
        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(1, result.TotalPages);
        var first = result.Items[0];
        Assert.AreEqual(a.ProductId, first.ProductId);
        Assert.AreEqual(7, first.OnHand);
        Assert.AreEqual(2, first.Reserved);
        Assert.AreEqual(5, first.Available);
    }

    [TestMethod]
    public async Task GetPageAsync_SecondPage_ReturnsTheNextRecordAndPagingTotals()
    {
        foreach (var sku in new[] { "NUT-C", "NUT-A", "NUT-B" })
            await _repo.SaveAsync(
                new StockReadModel(Guid.NewGuid(), sku, 1, 0, "pcs", 1, DateTime.UtcNow),
                TestContext.CancellationToken
            );

        var result = await _repo.GetPageAsync(2, 1, TestContext.CancellationToken);

        Assert.AreEqual("NUT-B", result.Items.Single().Sku);
        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(1, result.PageSize);
        Assert.AreEqual(3, result.TotalPages);
    }

    [TestMethod]
    public async Task GetPageAsync_WhenThereAreNoRecords_ReturnsAnEmptyPage()
    {
        var result = await _repo.GetPageAsync(cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(result.Items);
        Assert.AreEqual(0, result.TotalItems);
    }

    public TestContext TestContext { get; set; }
}
