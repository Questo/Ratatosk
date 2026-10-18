namespace Ratatosk.Application.Ordering.Models;

/// <summary>The Order as it was right after one event in its stream.</summary>
public sealed record OrderHistoryEntry(
    int Version,
    string Event,
    DateTimeOffset OccurredAtUtc,
    OrderReadModel Order,
    IReadOnlyList<string> ReservedSkus
);
