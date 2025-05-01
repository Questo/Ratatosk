using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence;

public class AggregateRepository<T>(IEventStore eventStore, ISnapshotStore snapshotStore) : IAggregateRepository<T> where T : AggregateRoot, new()
{
    public async Task<Result<T>> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var streamName = StreamName.For<T>(id);
        Snapshot? snapshot = await snapshotStore.LoadSnapshotAsync(id, cancellationToken);

        int fromVersion = 0;
        T? snapshotAggregate = null;

        if (snapshot is not null && snapshot is T typedSnapshot)
        {
            snapshotAggregate = typedSnapshot;
            fromVersion = snapshotAggregate.Version;
        }

        var events = await eventStore.LoadEventsAsync(
            streamName,
            fromVersion + 1,
            cancellationToken
        );

        // Combine history: snapshot (if any) + new events
        if (snapshotAggregate is not null)
        {
            snapshotAggregate.LoadFromHistory(events);
            return Result<T>.Success(snapshotAggregate);
        }

        if (events.Count == 0)
            return Result<T>.Failure($"{typeof(T).Name} with ID {id} not found");

        return AggregateRoot.Rehydrate<T>(events);
    }

    public async Task SaveAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        var streamName = StreamName.For<T>(aggregate.Id);
        var uncommitted = aggregate.UncommittedEvents;
        await eventStore.AppendEventsAsync(streamName, uncommitted, aggregate.Version, cancellationToken);

        if (!aggregate.ShouldCreateSnapshot())
            return;

        var snapshot = aggregate.CreateSnapshot();
        if (snapshot == null)
            return;

        await snapshotStore.SaveSnapshotAsync(snapshot, cancellationToken);
    }
}