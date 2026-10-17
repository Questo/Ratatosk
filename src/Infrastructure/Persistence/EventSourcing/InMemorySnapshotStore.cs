using System.Collections.Concurrent;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

// Must be registered as a singleton, like InMemoryEventStore.
public class InMemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<(string AggregateType, Guid AggregateId), Snapshot> _store =
        new();

    public Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        _store[(snapshot.AggregateType, snapshot.AggregateId)] = snapshot;
        return Task.CompletedTask;
    }

    public Task<Snapshot?> LoadSnapshotAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken = default
    )
    {
        _store.TryGetValue((aggregateType, aggregateId), out var snapshot);
        return Task.FromResult(snapshot);
    }
}
