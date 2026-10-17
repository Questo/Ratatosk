using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence.Repositories;

/// <summary>
/// Loads aggregates from snapshot + events, and commits them: append in raise order with an
/// expected-version check, snapshot when due, end the transaction, clear, then publish.
/// This is the only place that sequence lives.
/// </summary>
public class AggregateRepository<T>(
    IEventStore eventStore,
    ISnapshotStore snapshotStore,
    IUnitOfWork uow,
    IEventBus eventBus
) : IAggregateRepository<T>
    where T : AggregateRoot, new()
{
    private static readonly string AggregateType = typeof(T).FullName!;

    public async Task<Result<T>> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var aggregate = new T();
        var fromVersion = 1;

        var snapshot = await snapshotStore.LoadSnapshotAsync(AggregateType, id, cancellationToken);
        var restoredFromSnapshot = snapshot is not null && aggregate.LoadFromSnapshot(snapshot);
        if (restoredFromSnapshot)
            fromVersion = snapshot!.Version + 1;

        var events = await eventStore.LoadEventsAsync(
            StreamName.For<T>(id),
            fromVersion,
            cancellationToken
        );

        if (!restoredFromSnapshot && events.Count == 0)
            return Result<T>.Failure($"{typeof(T).Name} with ID {id} not found");

        try
        {
            aggregate.LoadFromHistory(events);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Failed to rehydrate aggregate: {ex.Message}");
        }

        return Result<T>.Success(aggregate);
    }

    public async Task CommitAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        var events = aggregate.UncommittedEvents;
        if (events.Count == 0)
            return;

        try
        {
            await eventStore.AppendEventsAsync(
                StreamName.For<T>(aggregate.Id),
                events,
                aggregate.PersistedVersion,
                cancellationToken
            );

            if (aggregate.ShouldCreateSnapshot() && aggregate.CreateSnapshot() is { } snapshot)
            {
                snapshot.AggregateType = AggregateType;
                await snapshotStore.SaveSnapshotAsync(snapshot, cancellationToken);
            }

            // Commit before publishing: handlers run in their own scopes and read this state back.
            uow.Commit();
        }
        catch
        {
            uow.Rollback();
            throw;
        }

        aggregate.ClearUncommittedEvents();

        foreach (var domainEvent in events)
            await eventBus.PublishAsync(domainEvent, cancellationToken);
    }
}
