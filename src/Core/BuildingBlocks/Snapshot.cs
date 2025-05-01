namespace Ratatosk.Core.BuildingBlocks;

public abstract class Snapshot
{
    public Guid AggregateId { get; init; }
    public int Version { get; init; }
    public string AggregateType { get; set; } = default!;
    public DateTime TakenAtUtc { get; init; } = DateTime.UtcNow;
}