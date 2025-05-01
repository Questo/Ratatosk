using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public interface ISnapshotSerializer
{
    string Serialize(Snapshot snapshot);
    Snapshot Deserialize(string json);
}