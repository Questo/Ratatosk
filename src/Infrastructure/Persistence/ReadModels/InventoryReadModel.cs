using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class InventoryReadModelRepository(IUnitOfWork uow)
    : PostgresRepository(uow),
      IInventoryReadModelRepository
{
    public Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    ) =>
        QueryFirstOrDefaultAsync<StockReadModel>(
            """
                SELECT product_id, sku, on_hand, reserved, unit, version, last_updated_utc
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
                SELECT product_id, sku, on_hand, reserved, unit, version, last_updated_utc
                FROM inventory_stock_read_models
                WHERE sku = @Sku
            """,
            new { Sku = sku },
            cancellationToken
        );

    public Task<Pagination<StockReadModel>> GetPageAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    ) =>
        PagedQueryAsync<StockReadModel>(
            fromAndJoinsSql: "FROM inventory_stock_read_models",
            selectColumnsSql: "SELECT product_id, sku, on_hand, reserved, unit, version, last_updated_utc",
            whereSql: null,
            parameters: null,
            orderBySql: "ORDER BY sku, product_id",
            page: page,
            pageSize: pageSize,
            ct: cancellationToken
        );

    public Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
                INSERT INTO inventory_stock_read_models (product_id, sku, on_hand, reserved, unit, version, last_updated_utc)
                VALUES (@ProductId, @Sku, @OnHand, @Reserved, @Unit, @Version, @LastUpdatedUtc)
                ON CONFLICT (product_id) DO UPDATE SET
                    sku = EXCLUDED.sku,
                    on_hand = EXCLUDED.on_hand,
                    reserved = EXCLUDED.reserved,
                    unit = EXCLUDED.unit,
                    version = EXCLUDED.version,
                    last_updated_utc = EXCLUDED.last_updated_utc
                WHERE inventory_stock_read_models.version < EXCLUDED.version
            """,
            stock,
            cancellationToken
        );
}
