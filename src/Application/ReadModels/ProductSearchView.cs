namespace Ratatosk.Application.ReadModels;

public class ProductSearchViewModel
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; } = default!;
}

public class ProductSearchView
{
    private readonly List<ProductSearchViewModel> _productList = [];
    private readonly Dictionary<Guid, ProductSearchViewModel> _productDictionary = [];

    public void AddProduct(ProductSearchViewModel product)
    {
        _productList.Add(product);
        _productDictionary[product.ProductId] = product;
    }

    public ProductSearchViewModel? GetProductById(Guid productId)
    {
        return _productDictionary.ContainsKey(productId) ? _productDictionary[productId] : null;
    }

    public IEnumerable<ProductSearchViewModel> GetSortedByPricePoint(decimal pricePoint, bool ascending = true)
    {
        IQueryable<ProductSearchViewModel> query = _productList
            .Where(p => p.Price >= pricePoint)
            .AsQueryable();

        return ascending
            ? query.OrderBy(p => p.Price)
            : query.OrderByDescending(p => p.Price);
    }

    public IEnumerable<ProductSearchViewModel> GetSortedByPrice(bool ascending = true)
    {
        return ascending
            ? _productList.OrderBy(p => p.Price)
            : _productList.OrderByDescending(p => p.Price);
    }
}
