using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Serialization.Serializers;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Infrastructure;

// The same contract, checked against both local event store adapters.
public abstract class EventStoreContract
{
    protected abstract IEventStore Store { get; }

    [TestMethod]
    public async Task Append_NumbersEventsFromTheExpectedVersion_AndLoadReturnsThemInOrder()
    {
        await Store.AppendEventsAsync("s", [new TestEvent("a"), new TestEvent("b")], 0);
        await Store.AppendEventsAsync("s", [new TestEvent("c")], 2);

        var loaded = await Store.LoadEventsAsync("s");

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, loaded.Cast<TestEvent>().Select(e => e.Value).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, loaded.Select(e => e.Version).ToArray());
    }

    [TestMethod]
    public async Task Load_FromVersion_SkipsEarlierEvents()
    {
        await Store.AppendEventsAsync("s", [new TestEvent("a"), new TestEvent("b"), new TestEvent("c")], 0);

        var loaded = await Store.LoadEventsAsync("s", fromVersion: 3);

        Assert.AreEqual("c", ((TestEvent)loaded.Single()).Value);
    }

    [TestMethod]
    public async Task Append_AtAStaleVersion_ThrowsAndAppendsNothing()
    {
        await Store.AppendEventsAsync("s", [new TestEvent("a")], 0);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            Store.AppendEventsAsync("s", [new TestEvent("b")], 0)
        );

        Assert.HasCount(1, await Store.LoadEventsAsync("s"));
    }

    [TestMethod]
    public async Task Load_UnknownStream_IsEmpty()
    {
        Assert.IsEmpty(await Store.LoadEventsAsync("missing"));
    }
}

[TestClass]
public class InMemoryEventStoreTests : EventStoreContract
{
    protected override IEventStore Store { get; } = new InMemoryEventStore();
}

[TestClass]
public class FileEventStoreTests : EventStoreContract
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ratatosk-{Guid.NewGuid()}");
    private FileEventStore? _store;

    protected override IEventStore Store =>
        _store ??= new FileEventStore(
            new EventStoreOptions { Type = StoreType.File, FilePath = _directory },
            new JsonEventSerializer()
        );

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
