namespace Ratatosk.Application.Search;

public sealed record SearchHit(Guid Id, string Title, string Subtitle, string Kind);

/// <summary>One provider's hits; <paramref name="Failed"/> when the provider threw.</summary>
public sealed record SearchGroup(string Kind, string Label, IReadOnlyList<SearchHit> Hits, bool Failed);

public sealed record SearchResults(IReadOnlyList<SearchGroup> Groups);
