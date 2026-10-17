namespace Ratatosk.Core.BuildingBlocks;

public abstract class DomainEvent
{
    // init, not get-only, so a stored event keeps its original timestamp when deserialized.
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public int Version { get; set; }
}
