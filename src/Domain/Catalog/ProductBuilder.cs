using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Catalog;

public class ProductBuilder : IBuilder<Product>
{
    private ProductName _name = ProductName.Create("Default Product").Value!;
    private SKU _sku = SKU.Create(SkuGenerator.Generate("TEST")).Value!;
    private Description _description = Description.Create("Default Description").Value!;
    private Price _price = Price.Free();

    public ProductBuilder WithName(string name)
    {
        var result = ProductName.Create(name);
        if (result.IsFailure)
            throw new ArgumentException(result.Error!);

        _name = result.Value!;
        return this;
    }

    public ProductBuilder WithSku(string sku)
    {
        var result = SKU.Create(sku);
        if (result.IsFailure)
            throw new ArgumentException(result.Error!);

        _sku = result.Value!;
        return this;
    }

    public ProductBuilder WithDescription(string description)
    {
        var result = Description.Create(description);
        if (result.IsFailure)
            throw new ArgumentException(result.Error!);

        _description = result.Value!;
        return this;
    }

    public ProductBuilder WithPrice(decimal amount, string currency = "SEK")
    {
        var result = Price.Create(amount, currency);
        if (result.IsFailure)
            throw new ArgumentException(result.Error!);

        _price = result.Value!;
        return this;
    }

    public Product Build()
    {
        var product = new Product();

        var created = new ProductCreated(product.Id, _name, _sku, _description, _price);
        product.LoadFromHistory([created]);

        return product;
    }

    public IBuilder<Product> With<TValue>(string propertyName, TValue value)
    {
        var property = typeof(ProductBuilder).GetField(
            $"_{propertyName}",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        property?.SetValue(this, value);
        return this;
    }
}
