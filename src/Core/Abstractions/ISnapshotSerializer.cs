using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Core.Abstractions;

public interface ISnapshotSerializer
{
    string Serialize(Snapshot snapshot);
    Snapshot Deserialize(string json);
}
