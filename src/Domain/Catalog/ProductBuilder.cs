using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Domain.Catalog;

public class ProductBuilder : IBuilder<Product>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Default Product";
    private string _sku = "Default Sku";
    private string _description = "Default description";
    private decimal _price = 0;

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

    public ProductBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public Product Build()
    {
        var product = new Product();

        var created = new ProductAdded(_id, _name, _sku);
        product.LoadFromHistory([created]);

        // Raise a ProductUpdated event to set optional fields like Description/Price
        var updated = new ProductUpdated(_id, _name, _description, _price);
        product.LoadFromHistory([updated]);

        product.ClearUncommittedEvents();
        return product;
    }

    public IBuilder<Product> With<TValue>(string propertyName, TValue value)
    {
        var property = typeof(ProductBuilder).GetField($"_{propertyName}", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        property?.SetValue(this, value);
        return this;
    }
}