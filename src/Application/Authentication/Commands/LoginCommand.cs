using System.Security.Cryptography;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<TokenPair>>;

public sealed class LoginCommandHandler(
    IUserAuthRepository userAuthRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer
) : IRequestHandler<LoginCommand, Result<TokenPair>>
{
    private const int RefreshTokenExpiresInDays = 7;

    public async Task<Result<TokenPair>> HandleAsync(
        LoginCommand request,
        CancellationToken cancellationToken = default
    )
    {
        var userAuth = await userAuthRepository.GetByEmailAsync(request.Email, cancellationToken);
        var passwordResult = Password.Create(request.Password);
        var hashResult = PasswordHash.Create(userAuth != null ? userAuth.Hash : string.Empty);

        if (
            userAuth is null
            || passwordResult.IsFailure
            || hashResult.IsFailure
            || !passwordHasher.Verify(passwordResult.Value!, hashResult.Value!)
        )
        {
            return Result<TokenPair>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var tokenResult = tokenIssuer.IssueToken(userAuth.Email, userAuth.Role);
        if (tokenResult.IsFailure)
            return Result<TokenPair>.Failure(Errors.Authentication.InvalidToken.Message);

        var refreshToken = new RefreshToken(
            Token: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Email: userAuth.Email,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiresInDays)
        );

        await refreshTokenRepository.SaveAsync(refreshToken, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(tokenResult.Value!, refreshToken.Token));
    }
}
