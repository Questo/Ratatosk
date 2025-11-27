using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.Abstractions;

public interface IAggregateRepository<T>
    where T : AggregateRoot, new()
{
    Task<Result<T>> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(T aggregate, CancellationToken cancellationToken = default);
}
