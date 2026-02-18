using System.Data;
using Dapper;
using Ratatosk.Application.Authentication.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public class PostgresUserSummaryRepository(IUnitOfWork uow) : IUserAuthRepository
{
    static PostgresUserSummaryRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task<IEnumerable<UserAuth>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
                SELECT * FROM user_auth
            """;

        return await uow.Connection.QueryAsync<UserAuth>(
            new CommandDefinition(
                sql,
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<IEnumerable<UserAuth>> GetAllByRole(
        string role,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
              SELECT * FROM user_auth
              WHERE (@Role IS NOT NULL OR role = @role)
            """;

        return await uow.Connection.QueryAsync<UserAuth>(
            new CommandDefinition(
                sql,
                new { Role = role },
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
              SELECT * FROM user_auth
              WHERE (@Email IS NOT NULL OR email = @Email)
            """;

        return await uow.Connection.QueryFirstOrDefaultAsync<UserAuth>(
            new CommandDefinition(
                sql,
                new { Email = email },
                transaction: uow.Transaction,
                cancellationToken: cancellationToken
            )
        );
    }
}
