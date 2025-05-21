using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence;

public class PostgresProductReadModelRepository(IOptions<DatabaseOptions> options) : IProductReadModelRepository
{
    private readonly IDbConnection _db = new NpgsqlConnection(options.Value.ConnectionString);

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

        await _db.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken
        ));
    }

    public async Task<IEnumerable<ProductReadModel>> GetAllAsync(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder("""
            SELECT *
            FROM product_read_models
        """);

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql.AppendLine("""
                WHERE name ILIKE @searchPattern
                    OR sku ILIKE @searchPattern
                    OR description ILIKE @searchPattern
            """);

            parameters.Add("searchPattern", $"%{searchTerm}%");
        }

        sql.AppendLine(" ORDER BY last_updated_utc DESC");
        sql.AppendLine("OFFSET @offset LIMIT @limit");

        parameters.Add("offset", (page - 1) * pageSize);
        parameters.Add("limit", pageSize);

        return await _db.QueryAsync<ProductReadModel>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));
    }


    public async Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, sku, description, price, last_updated_utc
            FROM product_read_models
            WHERE id = @Id
        """;

        return await _db.QueryFirstOrDefaultAsync<ProductReadModel>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken
        ));
    }

    public async Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default)
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

        await _db.ExecuteAsync(new CommandDefinition(
            sql,
            product,
            cancellationToken: cancellationToken
        ));
    }
}
