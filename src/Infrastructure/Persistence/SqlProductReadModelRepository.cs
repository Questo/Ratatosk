using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence;

public class SqlProductReadModelRepository(IOptions<DatabaseOptions> options) : IProductReadModelRepository
{
    private readonly IDbConnection _db = new SqlConnection(options.Value.ConnectionString);

    public async Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Description, Price, LastUpdatedUtc
            FROM ProductReadModels
            WHERE Id = @Id
        """;

        return await _db.QueryFirstOrDefaultAsync<ProductReadModel>(sql, new { Id = id });
    }

    public async Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ProductReadModels (Id, Name, Description, Price, LastUpdatedUtc)
            VALUES (@Id, @Name, @Description, @Price, @LastUpdatedUtc)
            ON CONFLICT (Id) DO UPDATE SET
                Name = @Name,
                Description = @Description,
                Price = @Price,
                LastUpdatedUtc = @LastUpdatedUtc
        """;

        await _db.ExecuteAsync(sql, product);
    }
}