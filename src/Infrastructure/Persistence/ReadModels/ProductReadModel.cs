using System.Text;
using Dapper;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class ProductReadModelRepository(IUnitOfWork uow)
    : PostgresRepository(uow),
      IProductReadModelRepository
{
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
                DELETE FROM product_read_models
                WHERE id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

    public Task<Pagination<ProductReadModel>> GetAllAsync(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var fromSql = "FROM product_read_models";
        var selectSql = "SELECT *";
        var orderSql = "ORDER BY last_updated_utc DESC";

        string? whereSql = null;
        object? parameters = null;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereSql = """WHERE name ILIKE @searchPattern OR sku ILIKE @searchPattern OR description ILIKE @searchPattern""";
            parameters = new { searchPattern = $"%{searchTerm}%" };
        }

        return PagedQueryAsync<ProductReadModel>(
            fromAndJoinsSql: fromSql,
            selectColumnsSql: selectSql,
            whereSql: whereSql,
            parameters: parameters,
            orderBySql: orderSql,
            page: page,
            pageSize: pageSize,
            ct: cancellationToken
        );
    }

    public Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        QueryFirstOrDefaultAsync<ProductReadModel>(
            """
                SELECT id, name, sku, description, price, last_updated_utc
                FROM product_read_models
                WHERE id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

    public Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
                INSERT INTO product_read_models (id, name, sku, description, price, last_updated_utc)
                VALUES (@Id, @Name, @Sku, @Description, @Price, @LastUpdatedUtc)
                ON CONFLICT (Id) DO UPDATE SET
                    name = EXCLUDED.name,
                    sku = EXCLUDED.sku,
                    description = EXCLUDED.description,
                    price = EXCLUDED.price,
                    last_updated_utc = EXCLUDED.last_updated_utc
            """,
            product,
            cancellationToken
        );
}
