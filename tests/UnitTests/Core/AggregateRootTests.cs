using Ratatosk.Core.BuildingBlocks;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Core;

public class TestAggregate : AggregateRoot
{
    public List<string> AppliedValues { get; } = [];

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        if (domainEvent is TestEvent e)
            AppliedValues.Add(e.Value);
    }

    public void DoSomething(string value)
    {
        RaiseEvent(new TestEvent(value));
    }

    public override Snapshot? CreateSnapshot() => new TestSnapshot(AppliedValues.ToArray());
}

public class TestSnapshot(string[] Values) : Snapshot;

[TestClass]
public class AggregateRootTests
{
    [TestMethod]
    public void RaiseEvent_Should_Add_Event_And_Increment_Version()
    {
        var agg = new TestAggregate();

        agg.DoSomething("foo");
        var expected = new string[] { "foo" };

        Assert.AreEqual(1, agg.UncommittedEvents.Count);
        Assert.AreEqual(1, agg.Version);
        CollectionAssert.AreEqual(expected, agg.AppliedValues);
    }

    [TestMethod]
    public void ClearUncommittedEvents_Should_Empty_Events()
    {
        var agg = new TestAggregate();
        agg.DoSomething("test");

        agg.ClearUncommittedEvents();

        Assert.AreEqual(0, agg.UncommittedEvents.Count);
    }

    [TestMethod]
    public void PersistedVersion_Should_Exclude_Uncommitted_Events()
    {
        var agg = new TestAggregate();
        agg.DoSomething("1");
        agg.DoSomething("2");

        Assert.AreEqual(2, agg.Version);
        Assert.AreEqual(0, agg.PersistedVersion); // 2 total, 2 uncommitted
    }

    [TestMethod]
    public void ShouldCreateSnapshot_Should_Return_True_At_Frequency()
    {
        var agg = new TestAggregate();

        for (int i = 0; i < agg.SnapshotFrequency; i++)
            agg.DoSomething($"e{i}");

        Assert.AreEqual(agg.SnapshotFrequency, agg.Version);
        Assert.IsTrue(agg.ShouldCreateSnapshot());
    }

    [TestMethod]
    public void LoadFromHistory_Should_Apply_Events_And_Increment_Version()
    {
        var events = Enumerable.Range(1, 3)
            .Select(i => new TestEvent($"v{i}"))
            .Cast<DomainEvent>()
            .ToArray();

        var agg = new TestAggregate();
        agg.LoadFromHistory(events);

        Assert.AreEqual(3, agg.Version);
        var values = new string[] { "v1", "v2", "v3" };
        CollectionAssert.AreEqual(values, agg.AppliedValues);
    }

    [TestMethod]
    public void Rehydrate_Should_Reconstruct_From_History()
    {
        var events = new List<DomainEvent>
            {
                new TestEvent("x"),
                new TestEvent("y")
            };

        var result = AggregateRoot.Rehydrate<TestAggregate>(events);
        var expected = new string[] { "x", "y" };

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(expected, result.Value!.AppliedValues);
    }

    [TestMethod]
    public void Rehydrate_Should_Return_Failure_When_Empty()
    {
        var result = AggregateRoot.Rehydrate<TestAggregate>(Enumerable.Empty<DomainEvent>());

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("History cannot be null or empty", result.Error);
    }
}