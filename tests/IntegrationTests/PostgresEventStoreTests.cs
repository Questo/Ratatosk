using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.Persistence.EventStore;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresEventStoreTests
{
    private const string ConnectionString = "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private PostgresEventStore _eventStore = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = ConnectionString });
        var serializer = new JsonEventSerializer();
        _eventStore = new PostgresEventStore(options, serializer);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
            DROP TABLE IF EXISTS events;
            CREATE TABLE events (
                event_id UUID PRIMARY KEY,
                stream_name TEXT NOT NULL,
                event_type TEXT NOT NULL,
                event_data TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL,
                version INTEGER NOT NULL,
                UNIQUE (stream_name, version)
            );
        """);
    }

    [TestMethod]
    public async Task AppendAndLoadEvents_ShouldWork()
    {
        var streamName = "user-123";
        var events = new List<TestEvent>
        {
            new("NameUpdated", "Alice"),
            new("AgeUpdated", "35")
        };

        await _eventStore.AppendEventsAsync(streamName, events, startingVersion: 0);
        var loadedEvents = await _eventStore.LoadEventsAsync(streamName);

        Assert.AreEqual(2, loadedEvents.Count);
        var loaded = (TestEvent)loadedEvents.First();
        Assert.AreEqual("Alice", loaded.NewValue);
        Assert.AreEqual(3, loadedEvents.Sum(e => e.Version));
    }

    private class TestEvent(string eventName, string newValue) : DomainEvent
    {
        public string EventName { get; set; } = eventName;
        public string NewValue { get; set; } = newValue;
    }
}