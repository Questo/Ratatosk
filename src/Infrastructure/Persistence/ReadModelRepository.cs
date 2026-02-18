using Dapper;
using Ratatosk.Application.Authentication.ReadModels;
using Ratatosk.Application.Shared;

namespace Ratatosk.Infrastructure.Persistence;



public sealed class ReadModelRepository<T>(IUnitOfWork uow)
{
    static ReadModelRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectClause();

        return await uow.Connection.QueryAsync<T>(
            new CommandDefinition(
                sql,
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    private static string BuildSelectClause()
    {
        var tableName = typeof(T) switch
        {
            var t when t == typeof(UserAuth) => "user_auth",
            var t when t == typeof(UserSummary) => "user_summary",
            _ => throw new NotSupportedException(
               $"No table mapping defined for type {typeof(T).Name}")
        };

        return $"SELECT * FROM {tableName}";
    }

}
