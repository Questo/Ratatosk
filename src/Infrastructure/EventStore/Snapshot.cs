using System.Dynamic;

namespace Ratatosk.Infrastructure.EventStore;

public abstract class Snapshot
{
    public Guid AggregateId { get; init; }
    public int Version { get; init; }
    public DateTime TakenAtUtc { get; init; } = DateTime.UtcNow;
}