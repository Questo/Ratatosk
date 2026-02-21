using Ratatosk.Application.Authentication.ReadModels;
using Ratatosk.Application.Shared;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class UserAuthReadModel(IUnitOfWork uow)
    : ReadModelRepository(uow),
        IUserAuthRepository
{
    public Task DeleteAsync(string email, CancellationToken cancellationToken) =>
        ExecAsync(
            """
              DELETE FROM user_auth_read_models
              where email = @Email
            """,
            new { Email = email },
            cancellationToken
        );

    public Task<Pagination<UserAuth>> GetAllAsync(CancellationToken cancellationToken)
    {
        var fromSql = "FROM user_auth_read_models";
        var selectSql = "SELECT *";
        var orderSql = "ORDER BY email";

        return PagedQueryAsync<UserAuth>(
            fromAndJoinsSql: fromSql,
            selectColumnsSql: selectSql,
            whereSql: null,
            parameters: null,
            orderBySql: orderSql,
            page: 1,
            pageSize: int.MaxValue,
            ct: cancellationToken
        );
    }

    public Task<Pagination<UserAuth>> GetAllByRole(
        string role,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var fromSql = "FROM user_auth_read_models";
        var selectSql = "SELECT *";
        var orderSql = "ORDER BY email";
        var whereSql = """WHERE role = @Role""";
        var args = new { Role = role };

        return PagedQueryAsync<UserAuth>(
            fromAndJoinsSql: fromSql,
            selectColumnsSql: selectSql,
            whereSql: whereSql,
            parameters: args,
            orderBySql: orderSql,
            page: page,
            pageSize: pageSize,
            ct: cancellationToken
        );
    }

    public Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        QueryFirstOrDefaultAsync<UserAuth?>(
            """
                SELECT email, role, hash
                FROM user_auth_read_models
                WHERE email = @Email
            """,
            new { Email = email },
            cancellationToken
        );

    public Task SaveAsync(UserAuth userAuth, CancellationToken cancellationToken) =>
        ExecAsync(
            """
                INSERT INTO user_auth_read_models (email, role, hash)
                VALUES (@Email, @Role, @Hash)
                ON CONFLICT (email) DO UPDATE SET
                  email = EXCLUDED.email,
                  role = EXCLUDED.role,
                  hash = EXCLUDED.hash
            """,
            userAuth,
            cancellationToken
        );
}
