using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence;

public class PostgresSkuUniqueness(IOptions<DatabaseOptions> options) : ISkuUniqueness
{
    private readonly IDbConnection _db = new NpgsqlConnection(options.Value.ConnectionString);

    public async Task<bool> IsUniqueAsync(string sku, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM product_read_models
            WHERE sku = @Sku
        """;

        var count = await _db.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { Sku = sku },
            cancellationToken: cancellationToken
        ));

        return count == 0;
    }
}