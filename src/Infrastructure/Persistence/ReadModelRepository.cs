using System.Text;
using Dapper;
using Ratatosk.Application.Shared;

namespace Ratatosk.Infrastructure.Persistence;

public abstract class ReadModelRepository(IUnitOfWork uow)
{
    static ReadModelRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    protected Task<int> ExecAsync(
        string sql,
        object? args = null,
        CancellationToken ct = default
    ) =>
        uow.Connection.ExecuteAsync(
            new CommandDefinition(sql, args, uow.Transaction, cancellationToken: ct)
        );

    protected Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? args = null,
        CancellationToken ct = default
    ) =>
        uow.Connection.QueryAsync<T>(
            new CommandDefinition(sql, args, uow.Transaction, cancellationToken: ct)
        );

    protected Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? args = null,
        CancellationToken ct = default
    ) =>
        uow.Connection.QueryFirstOrDefaultAsync<T>(
            new CommandDefinition(sql, args, uow.Transaction, cancellationToken: ct)
        );

    protected Task<T?> ScalarAsync<T>(
        string sql,
        object? args = null,
        CancellationToken ct = default
    ) =>
        uow.Connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, args, uow.Transaction, cancellationToken: ct)
        );

    protected async Task<Pagination<T>> PagedQueryAsync<T>(
        string fromAndJoinsSql, // e.g. "FROM product_read_models"
        string selectColumnsSql, // e.g. "SELECT id, name, ..."
        string? whereSql, // e.g. "WHERE name ILIKE @pattern"
        object? parameters,
        string orderBySql, // e.g. "ORDER BY last_updated_utc DESC"
        int page,
        int pageSize,
        CancellationToken ct = default
    )
        where T : class
    {
        var offset = (page - 1) * pageSize;

        var countSql = new StringBuilder()
            .AppendLine("SELECT COUNT(*)")
            .AppendLine(fromAndJoinsSql);
        if (!string.IsNullOrWhiteSpace(whereSql))
            countSql.AppendLine(whereSql);

        var dataSql = new StringBuilder().AppendLine(selectColumnsSql).AppendLine(fromAndJoinsSql);
        if (!string.IsNullOrWhiteSpace(whereSql))
            dataSql.AppendLine(whereSql);

        dataSql.AppendLine(orderBySql);
        dataSql.AppendLine("OFFSET @offset LIMIT @limit");

        // Merge paging params in a safe way
        var dp = new DynamicParameters(parameters);
        dp.Add("offset", offset);
        dp.Add("limit", pageSize);

        var total = await ScalarAsync<int>(countSql.ToString(), dp, ct);
        var items = await QueryAsync<T>(dataSql.ToString(), dp, ct);

        return new Pagination<T>
        {
            Items = [.. items],
            TotalItems = total,
            Page = page,
            PageSize = pageSize,
        };
    }
}
