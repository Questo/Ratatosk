using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class SqlEventStore(IOptions<DatabaseOptions> options, IEventSerializer serializer) : IEventStore
{
    private readonly IDbConnection _db = new SqlConnection(options.Value.ConnectionString);

    public async Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, int startingVersion, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO EventStore (EventId, StreamName, EventType, EventData, CreatedAt, Version)
            VALUES (@EventId, @StreamName, @EventType, @EventData, @CreatedAt, @Version)
        """;

        var eventList = events.ToList();
        for (int i = 0; i < eventList.Count; i++)
        {
            var domainEvent = eventList[i];
            var version = startingVersion + i + 1;

            var eventData = serializer.Serialize(domainEvent);
            await _db.ExecuteAsync(sql, new
            {
                EventId = Guid.NewGuid(),
                StreamName = streamName,
                EventType = domainEvent.GetType().FullName ?? "Unknown",
                EventData = eventData,
                CreatedAt = DateTime.UtcNow,
                Version = version
            });
        }
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, int startingVersion = 0, CancellationToken cancellationToken = default)
    {
        var events = new List<DomainEvent>();

        const string sql = """
            SELECT EventData FROM EventStore
            WHERE StreamName = @StreamName AND Version > @StartingVersion
            ORDER BY Version ASC
        """;

        var eventJsonData = await _db.QueryAsync<string>(sql, new { StreamName = streamName, Version = startingVersion });
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