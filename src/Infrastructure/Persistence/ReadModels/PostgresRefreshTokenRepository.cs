using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class PostgresRefreshTokenRepository(IUnitOfWork uow)
    : PostgresRepository(uow), IRefreshTokenRepository
{
    public Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
            INSERT INTO refresh_tokens (token, email, expires_at)
            VALUES (@Token, @Email, @ExpiresAt)
            ON CONFLICT (token) DO NOTHING
            """,
            token,
            cancellationToken
        );

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        QueryFirstOrDefaultAsync<RefreshToken?>(
            """
            SELECT token, email, expires_at
            FROM refresh_tokens
            WHERE token = @Token
            """,
            new { Token = token },
            cancellationToken
        );

    public Task RevokeAsync(string token, CancellationToken cancellationToken = default) =>
        ExecAsync(
            """
            DELETE FROM refresh_tokens
            WHERE token = @Token
            """,
            new { Token = token },
            cancellationToken
        );
}
