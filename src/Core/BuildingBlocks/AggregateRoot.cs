using System.Text.Json.Serialization;

namespace Ratatosk.Core.BuildingBlocks;

public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public int Version { get; protected set; } = 0;

    public int PersistedVersion => Version - _uncommittedEvents.Count;

    /// <summary>
    /// Events raised since the aggregate was loaded, in the order they were raised.
    /// </summary>
    public IReadOnlyList<DomainEvent> UncommittedEvents => [.. _uncommittedEvents];

    /// <summary>
    /// The number of events between each snapshot. Can be overridden in derived classes.
    /// </summary>
    [JsonIgnore]
    public virtual int SnapshotFrequency => 25;

    /// <summary>
    /// True when the uncommitted events carry the aggregate across a snapshot boundary.
    /// </summary>
    public bool ShouldCreateSnapshot() =>
        Version / SnapshotFrequency > PersistedVersion / SnapshotFrequency;

    protected void RaiseEvent(DomainEvent domainEvent)
    {
        _uncommittedEvents.Add(domainEvent);
        ApplyEvent(domainEvent);
        Version++;
    }

    protected abstract void ApplyEvent(DomainEvent domainEvent);

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    public void LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyEvent(@event);
            Version++;
        }
    }

    public virtual Snapshot? CreateSnapshot() => null;

    /// <summary>
    /// Restores state captured by <see cref="CreateSnapshot"/>. Aggregates that create
    /// snapshots must override this; the default rejects the snapshot so the caller replays
    /// the full stream instead.
    /// </summary>
    protected virtual bool RestoreSnapshot(Snapshot snapshot) => false;

    public bool LoadFromSnapshot(Snapshot snapshot)
    {
        if (!RestoreSnapshot(snapshot))
            return false;

        Id = snapshot.AggregateId;
        Version = snapshot.Version;
        return true;
    }
}
