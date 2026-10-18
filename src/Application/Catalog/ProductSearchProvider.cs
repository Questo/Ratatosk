using System.Globalization;
using Ratatosk.Application.Search;

namespace Ratatosk.Application.Catalog;

/// <summary>Finds Products by name, SKU or description (the product list's own matching).</summary>
public sealed class ProductSearchProvider(IProductReadModelRepository repository) : ISearchProvider
{
    public string Kind => "product";
    public string Label => "Products";
    public int Order => 10;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var products = await repository.GetAllAsync(
            term,
            page: 1,
            pageSize: limit,
            cancellationToken: cancellationToken
        );

        return
        [
            .. products.Items.Select(p => new SearchHit(
                p.Id,
                p.Name,
                string.Create(CultureInfo.InvariantCulture, $"{p.Sku} · {p.Price:0.00}"),
                Kind
            )),
        ];
    }
}
