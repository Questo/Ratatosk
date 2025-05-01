using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.UnitTests;

[TestClass]
public sealed class FileEventStoreTests
{
    private FileEventStore _eventStore = null!;
    private readonly string _testEventsPath = "data/fileeventstore-tests";

    // Dummy event for testing
    private class TestEvent(string name) : DomainEvent
    {
        public string Name { get; } = name;
    }

    [TestInitialize]
    public void Setup()
    {
        var serializer = new JsonEventSerializer();
        var options = new EventStoreOptions { Type = EventStoreType.File, FilePath = _testEventsPath };
        _eventStore = new FileEventStore(options, serializer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_testEventsPath, true);
    }

    [TestMethod]
    public async Task AppendAndLoadEvents_ShouldPersistAndReturnEvents()
    {
        var streamName = "test-stream";
        var domainEvent = new TestEvent("Foobar");
        var events = new List<DomainEvent> { domainEvent };

        await _eventStore.AppendEventsAsync(streamName, events, 0);
        var loadedEvents = await _eventStore.LoadEventsAsync(streamName);

        Assert.AreEqual(1, loadedEvents.Count);
        var result = (TestEvent)loadedEvents.First();
        Assert.AreEqual(domainEvent.Name, result.Name);
    }
}
