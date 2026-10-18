using System.Globalization;
using Ratatosk.Application.Search;

namespace Ratatosk.Application.Ordering;

/// <summary>Finds Orders by (partial) id, customer id, status or the SKU of any line.</summary>
public sealed class OrderSearchProvider(IOrderReadModelRepository repository) : ISearchProvider
{
    public string Kind => "order";
    public string Label => "Orders";
    public int Order => 20;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var orders = await repository.SearchAsync(term, limit, cancellationToken);

        return
        [
            .. orders.Select(o => new SearchHit(
                o.Id,
                o.Id.ToString("D")[..8],
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{o.Status} · {o.Lines.Count} {(o.Lines.Count == 1 ? "line" : "lines")} · {o.CreatedUtc:yyyy-MM-dd}"
                ),
                Kind
            )),
        ];
    }
}
