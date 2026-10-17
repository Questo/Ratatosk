using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.Abstractions;

public interface IAggregateRepository<T>
    where T : AggregateRoot, new()
{
    Task<Result<T>> LoadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the aggregate's uncommitted events (in the order they were raised), commits
    /// them, clears them from the aggregate, and then publishes them to event handlers.
    /// Handlers see committed state; a handler failure never undoes this commit.
    /// Throws <see cref="ConcurrencyException"/> if the stream moved on since the aggregate
    /// was loaded; nothing is persisted in that case.
    /// </summary>
    Task CommitAsync(T aggregate, CancellationToken cancellationToken = default);
}
