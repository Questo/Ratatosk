using Microsoft.Data.SqlClient;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.EventStore;

public class SqlEventStore(EventStoreOptions options, IEventSerializer serializer) : IEventStore
{
    public async Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, int startingVersion, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var eventList = events.ToList();
        for (int i = 0; i < eventList.Count; i++)
        {
            var domainEvent = eventList[i];
            var version = startingVersion + i + 1;

            var eventData = serializer.Serialize(domainEvent);
            var command = new SqlCommand(@"
                INSERT INTO EventStore (EventId, StreamName, EventType, EventData, CreatedAt, Version)
                VALUES (@EventId, @StreamName, @EventType, @EventData, @CreatedAt, @Version)", connection);

            command.Parameters.AddWithValue("@EventId", Guid.NewGuid());
            command.Parameters.AddWithValue("@StreamName", streamName);
            command.Parameters.AddWithValue("@EventType", domainEvent.GetType().FullName ?? "Unknown");
            command.Parameters.AddWithValue("@EventData", eventData);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@Version", version);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, int startingVersion = 0, CancellationToken cancellationToken = default)
    {
        var events = new List<DomainEvent>();

        using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
            SELECT EventData FROM EventStore
            WHERE StreamName = @StreamName AND Version > @StartingVersion
            ORDER BY Version ASC", connection);

        command.Parameters.AddWithValue("@StreamName", streamName);
        command.Parameters.AddWithValue("@StartingVersion", startingVersion);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var eventDataJson = reader.GetString(0);
            var domainEvent = serializer.Deserialize(eventDataJson);

            if (domainEvent == null)
            {
                continue;
            }

            events.Add(domainEvent);
        }

        return events;
    }
}