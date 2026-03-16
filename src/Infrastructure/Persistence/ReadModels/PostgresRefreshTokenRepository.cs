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

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var row = await QueryFirstOrDefaultAsync<RefreshTokenRow?>(
            """
            SELECT token, email, expires_at
            FROM refresh_tokens
            WHERE token = @Token
            """,
            new { Token = token },
            cancellationToken
        );

        return row is null ? null : new RefreshToken(row.Token, row.Email, row.ExpiresAt);
    }

    private sealed class RefreshTokenRow
    {
        public string Token { get; init; } = default!;
        public string Email { get; init; } = default!;
        public DateTimeOffset ExpiresAt { get; init; }
    }

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
