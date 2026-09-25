using Ratatosk.Application.Inventoring;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain;
using Ratatosk.Domain.Inventoring;

namespace Ratatosk.Infrastructure.Services;

public class InventoryDomainService(
    IAggregateRepository<Inventory> repository,
    IInventoryReadModelRepository readModelRepository,
    IEventBus eventBus
) : IInventoryDomainService
{
    public async Task<bool> IsProductInStockAsync(
        Guid productId,
        int? quantity = null,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetByProductIdAsync(productId, cancellationToken);
        if (readModel is null)
            return false;

        var skuResult = SKU.Create(readModel.Sku);
        if (skuResult.IsFailure)
            return false;

        var inventoryResult = await repository.LoadAsync(productId, cancellationToken);
        if (inventoryResult.IsFailure)
            return false;

        return inventoryResult.Value!.IsInStock(skuResult.Value!, quantity ?? 1);
    }

    public async Task<bool> IsProductInStockAsync(
        string sku,
        int? quantity = null,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetBySkuAsync(sku, cancellationToken);
        if (readModel is null)
            return false;

        return await IsProductInStockAsync(readModel.ProductId, quantity, cancellationToken);
    }

    public Task<Result> ReserveProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    ) => MutateAsync(productId, (inventory, sku) => inventory.ReserveStock(sku, quantity), cancellationToken);

    public async Task<Result> ReserveProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetBySkuAsync(sku, cancellationToken);
        if (readModel is null)
            return Result.Failure($"SKU {sku} not found");

        return await ReserveProductAsync(readModel.ProductId, quantity, cancellationToken);
    }

    public Task<Result> UnreserveProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    ) => MutateAsync(productId, (inventory, sku) => inventory.ReleaseStock(sku, quantity), cancellationToken);

    public async Task<Result> UnreserveProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetBySkuAsync(sku, cancellationToken);
        if (readModel is null)
            return Result.Failure($"SKU {sku} not found");

        return await UnreserveProductAsync(readModel.ProductId, quantity, cancellationToken);
    }

    public async Task<Result> RestockProductAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetByProductIdAsync(productId, cancellationToken);
        if (readModel is null)
            return Result.Failure($"No stock record found for product {productId}");

        var quantityResult = Quantity.Create(quantity, readModel.Unit);
        if (quantityResult.IsFailure)
            return Result.Failure(quantityResult.Error!);

        return await MutateAsync(
            productId,
            (inventory, sku) => inventory.AddStock(sku, quantityResult.Value!),
            cancellationToken
        );
    }

    public async Task<Result> RestockProductAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = await readModelRepository.GetBySkuAsync(sku, cancellationToken);
        if (readModel is null)
            return Result.Failure($"SKU {sku} not found");

        return await RestockProductAsync(readModel.ProductId, quantity, cancellationToken);
    }

    private async Task<Result> MutateAsync(
        Guid productId,
        Action<Inventory, SKU> mutate,
        CancellationToken cancellationToken
    )
    {
        var readModel = await readModelRepository.GetByProductIdAsync(productId, cancellationToken);
        if (readModel is null)
            return Result.Failure($"No stock record found for product {productId}");

        var skuResult = SKU.Create(readModel.Sku);
        if (skuResult.IsFailure)
            return Result.Failure(skuResult.Error!);

        var inventoryResult = await repository.LoadAsync(productId, cancellationToken);
        if (inventoryResult.IsFailure)
            return Result.Failure(inventoryResult.Error!);

        var inventory = inventoryResult.Value!;

        try
        {
            mutate(inventory, skuResult.Value!);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        await repository.SaveAsync(inventory, cancellationToken);

        foreach (var raised in inventory.UncommittedEvents)
            await eventBus.PublishAsync(raised, cancellationToken);

        return Result.Success();
    }
}
