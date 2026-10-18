using Microsoft.AspNetCore.Mvc;
using Ratatosk.Application.Search;
using Ratatosk.Application.Shared;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.API.Search;

public static class SearchEndpoints
{
    private const string SearchTag = "Search";

    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/search",
                async (
                    [FromQuery] string? q,
                    IDispatcher dispatcher,
                    CancellationToken ct,
                    [FromQuery] int limit = 5
                ) =>
                {
                    var result = await dispatcher.DispatchAsync(new SearchQuery(q, limit), ct);
                    var response = Response<SearchResults>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(SearchTag)
            .WithName("Search")
            .WithSummary("Search Products and Orders")
            .WithDescription(
                "Searches every kind of entity for q (trimmed, 2 to 100 characters) and returns one group "
                    + "per kind in a stable order, each with at most limit hits (default 5, at most 10). "
                    + "A group whose search failed comes back empty with failed set. Admin only."
            )
            .Produces<Response<SearchResults>>(StatusCodes.Status200OK)
            .Produces<Response<SearchResults>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
