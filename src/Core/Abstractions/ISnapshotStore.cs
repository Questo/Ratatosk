using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public interface ISnapshotStore
{
    Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken);
    Task<Snapshot?> LoadSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken);
}
