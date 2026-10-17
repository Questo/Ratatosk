using Dapper;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class SchemaUpgradeTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        // The tables as an older 01-init.sql created them.
        await _uow.Connection.ExecuteAsync(
            """
                DROP TABLE IF EXISTS snapshots;
                CREATE TABLE snapshots(
                    aggregate_id uuid PRIMARY KEY,
                    version integer NOT NULL,
                    aggregate_type text NOT NULL,
                    aggregate_data text NOT NULL,
                    taken_at_utc timestamptz NOT NULL
                );
                DROP TABLE IF EXISTS inventory_stock_read_models;
                CREATE TABLE inventory_stock_read_models(
                    product_id uuid PRIMARY KEY,
                    sku text NOT NULL,
                    available integer NOT NULL,
                    reserved integer NOT NULL,
                    unit text NOT NULL,
                    last_updated_utc timestamptz NOT NULL,
                    UNIQUE (sku)
                );
                INSERT INTO inventory_stock_read_models VALUES
                    ('6f1f6c43-1d1f-4a55-9d0c-1f2b3c4d5e6f', 'NUT-001', 10, 2, 'pcs', now());
            """,
            transaction: _uow.Transaction
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task Apply_KeysSnapshotsByTypeAndId_AndIsSafeToRunAgain()
    {
        await SchemaUpgrade.ApplyAsync(_uow.Connection, _uow.Transaction);
        await SchemaUpgrade.ApplyAsync(_uow.Connection, _uow.Transaction);

        var keyColumns = await _uow.Connection.QueryAsync<string>(
            """
                SELECT a.attname
                FROM pg_constraint c
                JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
                WHERE c.conrelid = 'snapshots'::regclass AND c.contype = 'p'
                ORDER BY a.attname
            """,
            transaction: _uow.Transaction
        );
        CollectionAssert.AreEqual(new[] { "aggregate_id", "aggregate_type" }, keyColumns.ToArray());
    }

    [TestMethod]
    public async Task Apply_RenamesAvailableToOnHand_AndAddsAVersion()
    {
        await SchemaUpgrade.ApplyAsync(_uow.Connection, _uow.Transaction);
        await SchemaUpgrade.ApplyAsync(_uow.Connection, _uow.Transaction);

        var row = await _uow.Connection.QuerySingleAsync<(int OnHand, int Reserved, int Version)>(
            "SELECT on_hand, reserved, version FROM inventory_stock_read_models WHERE sku = 'NUT-001'",
            transaction: _uow.Transaction
        );
        Assert.AreEqual((10, 2, 0), row);
    }
}
