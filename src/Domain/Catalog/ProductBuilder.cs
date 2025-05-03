using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog;

public class ProductBuilder : IBuilder<Product>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Default Product";
    private SKU _sku = default!;
    private Description _description = default!;
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
        var result = SKU.Create(sku);
        if (result.IsFailure) throw new ArgumentException(result.Error!);

        _sku = result.Value!;
        return this;
    }

    public ProductBuilder WithDescription(string description)
    {
        var result = Description.Create(description);
        if (result.IsFailure) throw new ArgumentException(result.Error!);

        _description = result.Value!;
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

        var created = new ProductCreated(_id, _name, _sku, _description, _price);
        product.LoadFromHistory([created]);

        return product;
    }

    public IBuilder<Product> With<TValue>(string propertyName, TValue value)
    {
        var property = typeof(ProductBuilder).GetField($"_{propertyName}", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        property?.SetValue(this, value);
        return this;
    }
}