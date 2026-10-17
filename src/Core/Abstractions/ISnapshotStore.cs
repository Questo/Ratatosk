using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Core.Abstractions;

/// <summary>
/// Stores the latest snapshot per aggregate. Snapshots are keyed by aggregate type as well as
/// id, because different aggregate types may share an id (an Inventory's id is its Product's id).
/// </summary>
public interface ISnapshotStore
{
    Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
    Task<Snapshot?> LoadSnapshotAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken = default
    );
}
