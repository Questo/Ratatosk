using System.Data;
using Dapper;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class PostgresEventStore(IDbConnection connection, IDbTransaction transaction, IEventSerializer serializer) : IEventStore
{
    public async Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, int startingVersion, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO events (event_id, stream_name, event_type, event_data, created_at, version)
            VALUES (@EventId, @StreamName, @EventType, @EventData, @CreatedAt, @Version)
        """;

        var eventList = events.ToList();
        for (int i = 0; i < eventList.Count; i++)
        {
            var domainEvent = eventList[i];
            var version = startingVersion + i + 1;

            domainEvent.Version = version;

            var eventData = serializer.Serialize(domainEvent);
            await connection.ExecuteAsync(sql, new
            {
                EventId = Guid.NewGuid(),
                StreamName = streamName,
                EventType = domainEvent.GetType().FullName ?? "Unknown",
                EventData = eventData,
                CreatedAt = DateTime.UtcNow,
                Version = version
            });
            transaction.Commit();
        }
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, int startingVersion = 0, CancellationToken cancellationToken = default)
    {
        var events = new List<DomainEvent>();

        const string sql = """
            SELECT event_data FROM events
            WHERE stream_name = @StreamName AND version > @StartingVersion
            ORDER BY version ASC
        """;

        var eventJsonData = await connection.QueryAsync<string>(sql, new { StreamName = streamName, StartingVersion = startingVersion });
        foreach (var jsonData in eventJsonData)
        {
            var domainEvent = serializer.Deserialize(jsonData);
            if (domainEvent == null)
            {
                continue;
            }

            events.Add(domainEvent);
        }

        return [.. events];
    }
}
