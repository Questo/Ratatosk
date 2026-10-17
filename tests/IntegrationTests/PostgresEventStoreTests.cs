using Dapper;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Serialization.Serializers;

namespace Ratatosk.IntegrationTests;

// One test commits its CREATE TABLE events; a sibling creating the same table
// concurrently then fails with 23505 on pg_type_typname_nsp_index.
[TestClass]
[DoNotParallelize]
public class PostgresEventStoreTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";

    private UnitOfWork _uow = null!;
    private PostgresEventStore _eventStore = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        var serializer = new JsonEventSerializer();

        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        await _uow.Connection.ExecuteAsync(
            """
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
            """,
            transaction: _uow.Transaction
        );

        _eventStore = new PostgresEventStore(_uow, serializer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_uow == null)
        {
            return;
        }

        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task AppendAndLoadEvents_ShouldWork()
    {
        var streamName = "user-123";
        var events = new List<TestEvent> { new("NameUpdated", "Alice"), new("AgeUpdated", "35") };

        await _eventStore.AppendEventsAsync(
            streamName,
            events,
            expectedVersion: 0,
            TestContext.CancellationToken
        );
        var loadedEvents = await _eventStore.LoadEventsAsync(
            streamName,
            cancellationToken: TestContext.CancellationToken
        );

        Assert.HasCount(2, loadedEvents);
        var loaded = (TestEvent)loadedEvents.First();
        Assert.AreEqual("Alice", loaded.NewValue);
        Assert.AreEqual(3, loadedEvents.Sum(e => e.Version));
    }

    [TestMethod]
    public async Task Append_AtAStaleVersion_ThrowsConcurrencyException_AndRollbackLeavesTheUnitOfWorkUsable()
    {
        await _eventStore.AppendEventsAsync("order-1", [new TestEvent("Placed", "1")], 0, TestContext.CancellationToken);
        _uow.Commit();

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _eventStore.AppendEventsAsync("order-1", [new TestEvent("Placed", "again")], 0, TestContext.CancellationToken)
        );
        _uow.Rollback();

        await _eventStore.AppendEventsAsync("order-1", [new TestEvent("Confirmed", "2")], 1, TestContext.CancellationToken);
        var loaded = await _eventStore.LoadEventsAsync("order-1", cancellationToken: TestContext.CancellationToken);
        CollectionAssert.AreEqual(new[] { "1", "2" }, loaded.Cast<TestEvent>().Select(e => e.NewValue).ToArray());
    }

    private class TestEvent(string eventName, string newValue) : DomainEvent
    {
        public string EventName { get; set; } = eventName;
        public string NewValue { get; set; } = newValue;
    }

    public TestContext TestContext { get; set; }
}
