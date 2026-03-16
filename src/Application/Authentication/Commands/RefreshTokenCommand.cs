using System.Security.Cryptography;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record RefreshTokenCommand(string Token) : IRequest<Result<TokenPair>>;

public sealed class RefreshTokenCommandHandler(
    IUserAuthRepository userAuthRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenIssuer tokenIssuer
) : IRequestHandler<RefreshTokenCommand, Result<TokenPair>>
{
    private const int RefreshTokenExpiresInDays = 7;

    public async Task<Result<TokenPair>> HandleAsync(
        RefreshTokenCommand request,
        CancellationToken cancellationToken = default
    )
    {
        var stored = await refreshTokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (stored is null || stored.IsExpired)
            return Result<TokenPair>.Failure(Errors.Authentication.InvalidRefreshToken.Message);

        var userAuth = await userAuthRepository.GetByEmailAsync(stored.Email, cancellationToken);
        if (userAuth is null)
            return Result<TokenPair>.Failure(Errors.Authentication.InvalidRefreshToken.Message);

        // Rotate: revoke old token before issuing new pair
        await refreshTokenRepository.RevokeAsync(request.Token, cancellationToken);

        var tokenResult = tokenIssuer.IssueToken(userAuth.Email, userAuth.Role);
        if (tokenResult.IsFailure)
            return Result<TokenPair>.Failure(Errors.Authentication.InvalidToken.Message);

        var newRefreshToken = new RefreshToken(
            Token: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Email: userAuth.Email,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiresInDays)
        );

        await refreshTokenRepository.SaveAsync(newRefreshToken, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(tokenResult.Value!, newRefreshToken.Token));
    }
}
