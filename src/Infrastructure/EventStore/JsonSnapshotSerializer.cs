using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.EventStore;

public class JsonSnapshotSerializer : JsonPolymorphicSerializer<Snapshot>, ISnapshotSerializer
{
}