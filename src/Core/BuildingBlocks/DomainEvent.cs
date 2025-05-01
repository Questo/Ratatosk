namespace Ratatosk.Core.BuildingBlocks;

public abstract class DomainEvent
{
    public Guid AggregateId { get; init; }
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public int Version { get; set; }
}