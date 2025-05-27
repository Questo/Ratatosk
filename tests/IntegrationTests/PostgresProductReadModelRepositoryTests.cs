using System.Numerics;
using Dapper;
using Npgsql;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresProductReadModelRepositoryTests
{
    private const string ConnectionString = "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;
    private PostgresProductReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        // Ensure tables exists, then truncate
        await _uow.Connection.ExecuteAsync("""
            DROP TABLE IF EXISTS product_read_models;
            CREATE TABLE IF NOT EXISTS product_read_models(
                id uuid PRIMARY KEY,
                name text NOT NULL,
                sku text NOT NULL,
                description text NOT NULL,
                price decimal NOT NULL,
                last_updated_utc timestamptz NOT NULL,
                UNIQUE (id, sku)
            );
            TRUNCATE TABLE product_read_models;
        """, transaction: _uow.Transaction);

        _repo = new PostgresProductReadModelRepository(_uow);
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
    public async Task SaveAsync_ShouldWork()
    {
        var productModel = new ProductReadModel(
            Guid.NewGuid(),
            "Acorn",
            "NUT-001",
            "A basic acorn",
            10.0m,
            DateTime.UtcNow
        );

        await _repo.SaveAsync(productModel);
    }

    [TestMethod]
    public async Task GetAllAsync_ShouldReturnAllProducts()
    {
        await _uow.Connection.ExecuteAsync("""
            INSERT INTO product_read_models (
                id, name, sku,
                description, price,
                last_updated_utc
            ) VALUES
            (
                '11111111-1111-1111-1111-111111111111',
                'Ratatosk Product A',
                'RAT-A-001',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            ),
            (
                '22222222-1111-1111-1111-111111111111',
                'Ratatosk Product B',
                'RAT-B-002',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            )
        """, transaction: _uow.Transaction);

        var searchResult = await _repo.GetAllAsync();

        Assert.AreEqual(2, searchResult.TotalItems);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenUsingSearchTerm_ShouldReturnExpectedProducts()
    {
        await _uow.Connection.ExecuteAsync("""
            INSERT INTO product_read_models (
                id, name, sku,
                description, price,
                last_updated_utc
            ) VALUES
            (
                '31111111-1111-1111-1111-111111111111',
                'Ratatosk Product A',
                'RAT-A-001',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            ),
            (
                '42222222-1111-1111-1111-111111111111',
                'Ratatosk Product B',
                'RAT-B-002',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            )
        """, transaction: _uow.Transaction);

        var searchResult = await _repo.GetAllAsync("Product B");

        Assert.AreEqual(1, searchResult.Items.Count);
        Assert.AreEqual("Ratatosk Product B", searchResult.Items.First().Name);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenSelectedSpecificPage_ShouldReturnExpectedProducts()
    {
        await _uow.Connection.ExecuteAsync("""
            INSERT INTO product_read_models (
                id, name, sku,
                description, price,
                last_updated_utc
            ) VALUES
            (
                '31111111-1111-1111-1111-111111111111',
                'Ratatosk Product A',
                'RAT-A-001',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            ),
            (
                '42222222-1111-1111-1111-111111111111',
                'Ratatosk Product B',
                'RAT-B-002',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            ),
            (
                '52222222-1111-1111-1111-111111111111',
                'Ratatosk Product C',
                'RAT-B-003',
                'Lightweight and nimble widget for fast squirrel logistics.',
                29.99,
                NOW()
            )
        """, transaction: _uow.Transaction);

        var searchResult = await _repo.GetAllAsync(page: 3, pageSize: 1);

        Assert.AreEqual(1, searchResult.Items.Count);
        Assert.AreEqual(3, searchResult.TotalItems);
        Assert.AreEqual(3, searchResult.TotalPages);
        Assert.AreEqual("Ratatosk Product C", searchResult.Items.First().Name);
    }
}
