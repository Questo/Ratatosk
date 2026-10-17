using Dapper;
using Npgsql;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class PostgresEventStore(IUnitOfWork uow, IEventSerializer serializer) : IEventStore
{
    public async Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
                INSERT INTO events (event_id, stream_name, event_type, event_data, created_at, version)
                VALUES (@EventId, @StreamName, @EventType, @EventData, @CreatedAt, @Version)
            """;

        var eventList = events.ToList();
        for (int i = 0; i < eventList.Count; i++)
        {
            var domainEvent = eventList[i];
            var version = expectedVersion + i + 1;

            domainEvent.Version = version;

            var eventData = serializer.Serialize(domainEvent);
            try
            {
                await uow.Connection.ExecuteAsync(
                    sql,
                    new
                    {
                        EventId = Guid.NewGuid(),
                        StreamName = streamName,
                        EventType = domainEvent.GetType().FullName ?? "Unknown",
                        EventData = eventData,
                        CreatedAt = DateTime.UtcNow,
                        Version = version,
                    },
                    uow.Transaction
                );
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // UNIQUE (stream_name, version): someone else already wrote this version.
                throw new ConcurrencyException(streamName, expectedVersion, ex);
            }

            // Don't commit here; the caller ends the transaction.
        }
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int fromVersion = 1,
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
                SELECT event_data FROM events
                WHERE stream_name = @StreamName AND version >= @FromVersion
                ORDER BY version ASC
            """;

        var eventJsonData = await uow.Connection.QueryAsync<string>(
            sql,
            new { StreamName = streamName, FromVersion = fromVersion },
            uow.Transaction
        );

        return [.. eventJsonData.Select(serializer.Deserialize)];
    }
}
