using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Domain.Catalog;

public class ProductBuilder : IBuilder<Product>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Default Product";
    private string _sku = "Default Sku";
    private string _description = "Default description";
    private Price _price = Price.Free();

    public ProductBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ProductBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProductBuilder WithSku(string sku)
    {
        _sku = sku;
        return this;
    }

    public ProductBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ProductBuilder WithPrice(decimal amount, string currency = "SEK")
    {
        var result = Price.Create(amount, currency);
        if (result.IsFailure) throw new ArgumentException(result.Error!);

        _price = result.Value!;
        return this;
    }

    public Product Build()
    {
        var product = new Product();

        var created = new ProductAdded(_id, _name, _sku, _price);
        product.LoadFromHistory([created]);

        // Raise a ProductUpdated event to set optional fields like Description/Price
        var updated = new ProductUpdated(_id, _name, _description, _price);
        product.LoadFromHistory([updated]);

        return product;
    }

    public IBuilder<Product> With<TValue>(string propertyName, TValue value)
    {
        var property = typeof(ProductBuilder).GetField($"_{propertyName}", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        property?.SetValue(this, value);
        return this;
    }
}