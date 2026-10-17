using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Core.Abstractions;

public interface IEventStore
{
    /// <summary>
    /// True when appended events last only if the unit of work commits (they vanish on rollback);
    /// false when an append is durable on its own, outside any transaction.
    /// </summary>
    bool AppendsInTransaction { get; }

    /// <summary>
    /// Appends events to the stream, numbering them from <paramref name="expectedVersion"/> + 1.
    /// Throws <see cref="ConcurrencyException"/> if the stream is not at <paramref name="expectedVersion"/>.
    /// </summary>
    Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Loads the stream's events with version &gt;= <paramref name="fromVersion"/>, oldest first.
    /// Versions start at 1.
    /// </summary>
    Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int fromVersion = 1,
        CancellationToken cancellationToken = default
    );
}
