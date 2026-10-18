using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Search;

/// <summary>
/// Searches every registered <see cref="ISearchProvider"/> for <paramref name="Term"/> and returns
/// one group per provider, at most <paramref name="Limit"/> hits each.
/// </summary>
public sealed record SearchQuery(string? Term, int Limit = 5) : IRequest<Result<SearchResults>>;

public class SearchQueryHandler(
    IEnumerable<ISearchProvider> providers,
    ILogger<SearchQueryHandler> logger
) : IRequestHandler<SearchQuery, Result<SearchResults>>
{
    public const int MinTermLength = 2;
    public const int MaxTermLength = 100;
    public const int MaxLimit = 10;

    public async Task<Result<SearchResults>> HandleAsync(
        SearchQuery request,
        CancellationToken cancellationToken = default
    )
    {
        var term = request.Term?.Trim();
        if (term is null || term.Length is < MinTermLength or > MaxTermLength)
            return Result<SearchResults>.Failure(
                $"Search term must be between {MinTermLength} and {MaxTermLength} characters."
            );

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var groups = new List<SearchGroup>();

        // One after another: the providers share the request's connection and unit of work.
        foreach (var provider in providers.OrderBy(p => p.Order).ThenBy(p => p.Kind, StringComparer.Ordinal))
        {
            try
            {
                var hits = await provider.SearchAsync(term, limit, cancellationToken);
                groups.Add(new SearchGroup(provider.Kind, provider.Label, hits, Failed: false));
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Search provider {Kind} failed for {Term}", provider.Kind, term);
                groups.Add(new SearchGroup(provider.Kind, provider.Label, [], Failed: true));
            }
        }

        return Result<SearchResults>.Success(new SearchResults(groups));
    }
}
