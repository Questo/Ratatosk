namespace Ratatosk.Application.Search;

/// <summary>
/// One kind of entity the universal search can find. Every concrete implementation in the
/// Application assembly is registered automatically, so adding a kind is adding a provider.
/// </summary>
public interface ISearchProvider
{
    /// <summary>The hit kind the client maps to a screen, e.g. "product".</summary>
    string Kind { get; }

    /// <summary>The group heading, e.g. "Products".</summary>
    string Label { get; }

    /// <summary>Where the group sorts among the others (ascending, then by <see cref="Kind"/>).</summary>
    int Order { get; }

    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken = default
    );
}
