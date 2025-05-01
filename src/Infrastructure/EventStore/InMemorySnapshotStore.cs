using System.Collections.Concurrent;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public class InMemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<Guid, Snapshot> _store = new();

    public Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        _store[snapshot.AggregateId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<Snapshot?> LoadSnapshotAsync(Guid aggregateid, CancellationToken cancellationToken)
    {
        _store.TryGetValue(aggregateid, out var snapshot);
        return Task.FromResult(snapshot);
    }
}