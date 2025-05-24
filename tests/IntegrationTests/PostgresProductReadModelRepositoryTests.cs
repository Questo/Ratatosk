using Dapper;
using Npgsql;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresProductReadModelRepositoryTests
{
    private const string ConnectionString = "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private NpgsqlConnection _conn = null!;
    private NpgsqlTransaction _transaction = null!;
    private PostgresProductReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _conn = new NpgsqlConnection(ConnectionString);
        await _conn.OpenAsync();
        _transaction = await _conn.BeginTransactionAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Ensure tables exists, then truncate
        await conn.ExecuteAsync("""
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
        """, transaction: _transaction);

        _repo = new PostgresProductReadModelRepository(_conn, _transaction);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _transaction.RollbackAsync();
        await _conn.DisposeAsync();
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
        await _conn.ExecuteAsync("""
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
        """, transaction: _transaction);

        var searchResult = await _repo.GetAllAsync();

        Assert.AreEqual(2, searchResult.TotalItems);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenUsingSearchTerm_ShouldReturnExpectedProducts()
    {
        await _conn.ExecuteAsync("""
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
        """, transaction: _transaction);

        var searchResult = await _repo.GetAllAsync("Product B");

        Assert.AreEqual(1, searchResult.Items.Count);
        Assert.AreEqual("Ratatosk Product B", searchResult.Items.First().Name);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenSelectedSpecificPage_ShouldReturnExpectedProducts()
    {
        await _conn.ExecuteAsync("""
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
        """, transaction: _transaction);

        var searchResult = await _repo.GetAllAsync(page: 3, pageSize: 1);

        Assert.AreEqual(1, searchResult.Items.Count);
        Assert.AreEqual(3, searchResult.TotalItems);
        Assert.AreEqual(3, searchResult.TotalPages);
        Assert.AreEqual("Ratatosk Product C", searchResult.Items.First().Name);
    }
}
