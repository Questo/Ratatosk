using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class SqlSnapshotStore(IOptions<DatabaseOptions> options, ISnapshotSerializer serializer) : ISnapshotStore
{
    private readonly IDbConnection _db = new SqlConnection(options.Value.ConnectionString);

    public async Task<Snapshot?> LoadSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SnapshotData FROM Snapshots
            WHERE AggregateId = @AggregateId
        """;

        var snapshotJsonData = await _db.QueryFirstOrDefaultAsync<string>(sql, new { Aggregateid = aggregateId });
        if (string.IsNullOrWhiteSpace(snapshotJsonData))
        {
            return null;
        }

        return serializer.Deserialize(snapshotJsonData);
    }

    public async Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE INTO Snapshots AS target
            USING (SELECT @AggregateId as AggregateId) AS source
            ON target.AggregateId = source.AggregateId
            WHEN MATCHED THEN
                UPDATE SET
                    Version = @Version,
                    AggregateType = @AggregateType,
                    SnapshotData = @SnapshotData,
                    Timestamp = @Timestamp
            WHEN NOT MATCHED THEN
                INSERT (Aggregateid, Version, AggregateType, SnapshotData, Timestamp)
                VALUES (@Aggregateid, @Version, @AggregateType, @SnapshotData, @Timestamp)
        """;

        await _db.ExecuteAsync(sql, new
        {
            AggregateId = snapshot.AggregateId,
            Version = snapshot.Version,
            AggregateType = snapshot.AggregateType,
            SnapshotData = serializer.Serialize(snapshot),
            TimeStamp = snapshot.TakenAtUtc
        });
    }
}