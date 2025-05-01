
using Microsoft.Data.SqlClient;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.EventStore;

public class SqlSnapshotStore(EventStoreOptions options, ISnapshotSerializer serializer) : ISnapshotStore
{
    public async Task<Snapshot?> LoadSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT SnapshotData FROM Snapshots
            WHERE AggregateId = @AggregateId";

        using var connection = new SqlConnection(options.ConnectionString);
        using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@AggregateId", aggregateId);

        await connection.OpenAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshotDataJson = reader.GetString(0);
        var snapshot = serializer.Deserialize(snapshotDataJson);

        return snapshot;
    }

    public async Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        const string sql = @"
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
                VALUES (@Aggregateid, @Version, @AggregateType, @SnapshotData, @Timestamp)";

        using var connection = new SqlConnection(options.ConnectionString);
        using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@AggregateId", snapshot.AggregateId);
        command.Parameters.AddWithValue("@Version", snapshot.Version);
        command.Parameters.AddWithValue("@AggregateType", snapshot.AggregateType);
        command.Parameters.AddWithValue("@SnapshotData", serializer.Serialize(snapshot));
        command.Parameters.AddWithValue("@Timestamp", snapshot.TakenAtUtc);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}