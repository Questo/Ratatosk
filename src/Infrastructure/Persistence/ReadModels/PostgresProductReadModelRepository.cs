using System.Data;
using System.Text;
using Dapper;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public class PostgresProductReadModelRepository(IUnitOfWork uow) : IProductReadModelRepository
{
    static PostgresProductReadModelRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
                DELETE FROM product_read_models
                WHERE id = @Id
            """;

        await uow.Connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { Id = id },
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<Pagination<ProductReadModel>> GetAllAsync(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        const string countSql = "SELECT COUNT(*) FROM product_read_models";
        var sql = new StringBuilder(
            """
                SELECT *
                FROM product_read_models
            """
        );

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql.AppendLine(
                """
                    WHERE name ILIKE @searchPattern
                        OR sku ILIKE @searchPattern
                        OR description ILIKE @searchPattern
                """
            );

            parameters.Add("searchPattern", $"%{searchTerm}%");
        }

        sql.AppendLine(" ORDER BY last_updated_utc DESC");
        sql.AppendLine("OFFSET @offset LIMIT @limit");

        parameters.Add("offset", (page - 1) * pageSize);
        parameters.Add("limit", pageSize);

        var totalItems = await uow.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                countSql,
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );

        var items = await uow.Connection.QueryAsync<ProductReadModel>(
            new CommandDefinition(
                sql.ToString(),
                parameters,
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );

        return new Pagination<ProductReadModel>
        {
            Items = [.. items],
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ProductReadModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
                SELECT id, name, sku, description, price, last_updated_utc
                FROM product_read_models
                WHERE id = @Id
            """;

        return await uow.Connection.QueryFirstOrDefaultAsync<ProductReadModel>(
            new CommandDefinition(
                sql,
                new { Id = id },
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task SaveAsync(
        ProductReadModel product,
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
                INSERT INTO product_read_models (id, name, sku, description, price, last_updated_utc)
                VALUES (@Id, @Name, @Sku, @Description, @Price, @LastUpdatedUtc)
                ON CONFLICT (Id) DO UPDATE SET
                    name = @Name,
                    sku = @Sku,
                    description = @Description,
                    price = @Price,
                    last_updated_utc = @LastUpdatedUtc
            """;

        await uow.Connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                product,
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }
}
