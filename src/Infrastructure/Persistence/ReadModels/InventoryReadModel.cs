using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class InventoryReadModelRepository(IUnitOfWork uow)
    : PostgresRepository(uow),
      IInventoryReadModelRepository
{
    public Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
                DELETE FROM inventory_stock_read_models
                WHERE product_id = @ProductId
            """,
            new { ProductId = productId },
            cancellationToken
        );

    public Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    ) =>
        QueryFirstOrDefaultAsync<StockReadModel>(
            """
                SELECT product_id, sku, available, reserved, unit, last_updated_utc
                FROM inventory_stock_read_models
                WHERE product_id = @ProductId
            """,
            new { ProductId = productId },
            cancellationToken
        );

    public Task<StockReadModel?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default
    ) =>
        QueryFirstOrDefaultAsync<StockReadModel>(
            """
                SELECT product_id, sku, available, reserved, unit, last_updated_utc
                FROM inventory_stock_read_models
                WHERE sku = @Sku
            """,
            new { Sku = sku },
            cancellationToken
        );

    public Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
                INSERT INTO inventory_stock_read_models (product_id, sku, available, reserved, unit, last_updated_utc)
                VALUES (@ProductId, @Sku, @Available, @Reserved, @Unit, @LastUpdatedUtc)
                ON CONFLICT (product_id) DO UPDATE SET
                    sku = EXCLUDED.sku,
                    available = EXCLUDED.available,
                    reserved = EXCLUDED.reserved,
                    unit = EXCLUDED.unit,
                    last_updated_utc = EXCLUDED.last_updated_utc
            """,
            stock,
            cancellationToken
        );
}
