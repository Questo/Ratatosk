using System.Data;
using Dapper;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class PostgresSnapshotStore(IUnitOfWork uow, ISnapshotSerializer serializer) : ISnapshotStore
{
    public async Task<Snapshot?> LoadSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT aggregate_data FROM Snapshots
            WHERE aggregate_id = @AggregateId
        """;

        var snapshotJsonData = await uow.Connection.QueryFirstOrDefaultAsync<string>(
            sql,
            new { AggregateId = aggregateId },
            uow.Transaction);

        if (string.IsNullOrWhiteSpace(snapshotJsonData))
        {
            return null;
        }

        return serializer.Deserialize(snapshotJsonData);
    }

    public async Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO snapshots (
                aggregate_id,
                version,
                aggregate_type,
                aggregate_data,
                taken_at_utc
            ) VALUES (
                @AggregateId,
                @Version,
                @AggregateType,
                @SnapshotData,
                @Timestamp
            )
            ON CONFLICT (aggregate_id) DO UPDATE SET
                version = EXCLUDED.version,
                aggregate_type = EXCLUDED.aggregate_type,
                aggregate_data = EXCLUDED.aggregate_data,
                taken_at_utc = EXCLUDED.taken_at_utc
        """;

        await uow.Connection.ExecuteAsync(sql, new
        {
            snapshot.AggregateId,
            snapshot.Version,
            snapshot.AggregateType,
            SnapshotData = serializer.Serialize(snapshot),
            TimeStamp = snapshot.TakenAtUtc
        }, uow.Transaction);

        // ❌ Don't commit here; let the caller commit via IUnitOfWork
    }
}
