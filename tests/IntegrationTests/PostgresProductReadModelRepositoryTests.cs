using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresProductReadModelRepositoryTests
{
    private const string ConnectionString = "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private PostgresProductReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = ConnectionString });
        _repo = new PostgresProductReadModelRepository(options);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
            DROP TABLE IF EXISTS product_read_models;
            CREATE TABLE product_read_models(
                id uuid PRIMARY KEY,
                name text NOT NULL,
                sku text NOT NULL,
                description text NOT NULL,
                price decimal NOT NULL,
                last_updated_utc timestamptz NOT NULL,
                UNIQUE (id, sku)
            );
        """);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
            DELETE FROM product_read_models;
        """);
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
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
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
        """);

        var searchResult = await _repo.GetAllAsync();

        Assert.AreEqual(2, searchResult.TotalItems);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenUsingSearchTerm_ShouldReturnExpectedProducts()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
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
        """);

        var searchResult = await _repo.GetAllAsync("Product B");

        Assert.AreEqual(1, searchResult.TotalItems);
    }
}
