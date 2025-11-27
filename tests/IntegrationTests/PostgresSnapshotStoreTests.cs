using Dapper;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Serialization;

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
                    aggregate_id uuid PRIMARY KEY,
                    version integer NOT NULL,
                    aggregate_type text NOT NULL,
                    aggregate_data text NOT NULL,
                    taken_at_utc timestamptz NOT NULL,
                    UNIQUE (aggregate_id, version)
                );
            """,
            transaction: _uow.Transaction
        );

        _snapshotStore = new PostgresSnapshotStore(_uow, serializer);
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
            aggregateId,
            CancellationToken.None
        );

        Assert.IsNotNull(loadedSnapshot);
        Assert.AreEqual(snapshot.AggregateId, loadedSnapshot.AggregateId);
        Assert.AreEqual(snapshot.Version, loadedSnapshot.Version);
        Assert.AreEqual(snapshot.AggregateType, loadedSnapshot.AggregateType);
    }

    private class TestSnapshot : Snapshot
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
