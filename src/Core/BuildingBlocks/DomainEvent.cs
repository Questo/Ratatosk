namespace Ratatosk.Core.BuildingBlocks;

public abstract class DomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public int Version { get; set; }
}