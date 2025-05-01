using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog.Events;

namespace Ratatosk.Application.ReadModels;

public class ProductSearchProjection(IProductSearchViewRepository repository)
{
    private readonly IProductSearchViewRepository _repository = repository;

    public async Task ProjectAsync(ProductAdded @event, CancellationToken cancellationToken = default)
    {
        var view = await _repository.GetViewAsync(cancellationToken);

        var product = new ProductSearchViewModel
        {
            ProductId = @event.ProductId,
            Name = @event.Name,
            Sku = @event.Sku
        };

        view.AddProduct(product);
        await _repository.SaveAsync(view, cancellationToken);
    }

    public async Task ProjectAsync(ProductUpdated @event, CancellationToken cancellationToken = default)
    {
        var view = await _repository.GetViewAsync(cancellationToken);

        var product = view.GetProductById(@event.ProductId);
        if (product is not null)
        {
            product.Name = @event.Name;
            product.Description = @event.Description;
            product.Price = @event.Price;

            await _repository.SaveAsync(view, cancellationToken);
        }
    }
}