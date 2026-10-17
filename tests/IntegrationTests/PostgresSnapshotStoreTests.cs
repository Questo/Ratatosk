using Dapper;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Serialization.Serializers;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresSnapshotStoreTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;
    private PostgresSnapshotStore _snapshotStore = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        var serializer = new JsonSnapshotSerializer();

        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        await _uow.Connection.ExecuteAsync(
            """
                DROP TABLE IF EXISTS snapshots;
                CREATE TABLE IF NOT EXISTS snapshots(
                    aggregate_id uuid NOT NULL,
                    version integer NOT NULL,
                    aggregate_type text NOT NULL,
                    aggregate_data text NOT NULL,
                    taken_at_utc timestamptz NOT NULL,
                    -- Keyed by type too: an Inventory shares its id with its Product.
                    PRIMARY KEY (aggregate_type, aggregate_id)
                );
            """,
            transaction: _uow.Transaction
        );

        _snapshotStore = new PostgresSnapshotStore(_uow, serializer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Each test's schema changes stay uncommitted; ending them releases the table locks.
        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task SaveAndLoadSnapshot_ShouldWork()
    {
        var aggregateId = Guid.NewGuid();
        var snapshot = new TestSnapshot
        {
            AggregateId = aggregateId,
            Version = 1,
            AggregateType = "TestAggregate",
            TakenAtUtc = DateTime.UtcNow,
        };

        // Save the snapshot
        await _snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None);

        // Load the snapshot
        var loadedSnapshot = await _snapshotStore.LoadSnapshotAsync(
            "TestAggregate",
            aggregateId,
            CancellationToken.None
        );

        Assert.IsNotNull(loadedSnapshot);
        Assert.AreEqual(snapshot.AggregateId, loadedSnapshot.AggregateId);
        Assert.AreEqual(snapshot.Version, loadedSnapshot.Version);
        Assert.AreEqual(snapshot.AggregateType, loadedSnapshot.AggregateType);
    }

    [TestMethod]
    public async Task Snapshots_OfDifferentAggregateTypes_WithTheSameId_DoNotOverwriteEachOther()
    {
        var sharedId = Guid.NewGuid();
        await _snapshotStore.SaveSnapshotAsync(
            new TestSnapshot { AggregateId = sharedId, Version = 25, AggregateType = "Product", Name = "product" },
            CancellationToken.None
        );
        await _snapshotStore.SaveSnapshotAsync(
            new TestSnapshot { AggregateId = sharedId, Version = 50, AggregateType = "Inventory", Name = "inventory" },
            CancellationToken.None
        );

        var product = (TestSnapshot?)await _snapshotStore.LoadSnapshotAsync("Product", sharedId, CancellationToken.None);
        var inventory = (TestSnapshot?)await _snapshotStore.LoadSnapshotAsync("Inventory", sharedId, CancellationToken.None);

        Assert.AreEqual("product", product!.Name);
        Assert.AreEqual("inventory", inventory!.Name);
    }

    private class TestSnapshot : Snapshot
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
