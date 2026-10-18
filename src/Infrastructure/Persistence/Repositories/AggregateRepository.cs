using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence.Repositories;

/// <summary>
/// Loads aggregates from snapshot + events, and commits them: append in raise order with an
/// expected-version check, end the transaction, clear, snapshot when due (best-effort, in its
/// own transaction), then publish. This is the only place that sequence lives.
/// </summary>
public class AggregateRepository<T>(
    IEventStore eventStore,
    ISnapshotStore snapshotStore,
    IUnitOfWork uow,
    IEventBus eventBus,
    ILogger<AggregateRepository<T>> logger
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

    public async Task<Result<IReadOnlyList<DomainEvent>>> LoadHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var events = await eventStore.LoadEventsAsync(StreamName.For<T>(id), 1, cancellationToken);

        if (events.Count == 0)
            return Result<IReadOnlyList<DomainEvent>>.Failure($"{typeof(T).Name} with ID {id} not found");

        return Result<IReadOnlyList<DomainEvent>>.Success([.. events]);
    }

    public async Task CommitAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        var events = aggregate.UncommittedEvents;
        if (events.Count == 0)
            return;

        var snapshotDue = aggregate.ShouldCreateSnapshot();
        var appended = false;

        try
        {
            await eventStore.AppendEventsAsync(
                StreamName.For<T>(aggregate.Id),
                events,
                aggregate.PersistedVersion,
                cancellationToken
            );
            appended = true;

            // Commit before publishing: handlers run in their own scopes and read this state back.
            uow.Commit();
        }
        catch (Exception ex)
        {
            uow.Rollback();

            // A store outside the transaction already holds the events; the read models must
            // still see them, even though the rest of the transaction is lost.
            if (appended && !eventStore.AppendsInTransaction)
            {
                logger.LogError(
                    ex,
                    "Transaction failed after {Count} event(s) were stored for {Aggregate} {Id}; publishing them anyway",
                    events.Count,
                    typeof(T).Name,
                    aggregate.Id
                );
                aggregate.ClearUncommittedEvents();
                await PublishAsync(events, cancellationToken);
            }

            throw;
        }

        aggregate.ClearUncommittedEvents();

        if (snapshotDue)
            await TrySaveSnapshotAsync(aggregate, cancellationToken);

        await PublishAsync(events, cancellationToken);
    }

    // A snapshot only speeds up loading, so failing to save one must not fail the commit. It
    // gets its own transaction: a failed statement would abort the one the events went into.
    private async Task TrySaveSnapshotAsync(T aggregate, CancellationToken cancellationToken)
    {
        if (aggregate.CreateSnapshot() is not { } snapshot)
            return;

        try
        {
            snapshot.AggregateType = AggregateType;
            await snapshotStore.SaveSnapshotAsync(snapshot, cancellationToken);
            uow.Commit();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                uow.Rollback();
            }
            catch { }

            logger.LogWarning(
                ex,
                "Could not save a snapshot of {Aggregate} {Id} at version {Version}",
                typeof(T).Name,
                aggregate.Id,
                snapshot.Version
            );
        }
    }

    private async Task PublishAsync(IReadOnlyList<DomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
            await eventBus.PublishAsync(domainEvent, cancellationToken);
    }
}
