using System.Security.Cryptography;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record SignUpCommand(string Email, string Password) : IRequest<Result<TokenPair>>;

public sealed class SignUpCommandHandler(
    IAggregateRepository<User> userRepository,
    IEventBus eventBus,
    IUserAuthRepository userAuthRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer
) : IRequestHandler<SignUpCommand, Result<TokenPair>>
{
    private const int RefreshTokenExpiresInDays = 7;

    public async Task<Result<TokenPair>> HandleAsync(
        SignUpCommand request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var userAuth = await userAuthRepository.GetByEmailAsync(
                request.Email,
                cancellationToken
            );
            if (userAuth is not null)
                return Result<TokenPair>.Failure(Errors.Authentication.AccountAlreadyExists.Message);

            var passwordResult = Password.Create(request.Password);
            if (passwordResult.IsFailure)
                return Result<TokenPair>.Failure(passwordResult.Error!);

            var passwordHash = passwordHasher.Hash(passwordResult.Value!);

            var user = User.Create(request.Email, UserRole.User, passwordHash.Value!);
            await userRepository.SaveAsync(user, cancellationToken);

            foreach (var domainEvent in user.UncommittedEvents)
                await eventBus.PublishAsync(domainEvent, cancellationToken);

            var tokenResult = tokenIssuer.IssueToken(user.Email.Value, user.Role.Name);
            if (tokenResult.IsFailure)
                return Result<TokenPair>.Failure(Errors.Authentication.InvalidToken.Message);

            var refreshToken = new RefreshToken(
                Token: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                Email: user.Email.Value,
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiresInDays)
            );

            await refreshTokenRepository.SaveAsync(refreshToken, cancellationToken);

            return Result<TokenPair>.Success(new TokenPair(tokenResult.Value!, refreshToken.Token));
        }
        catch (Exception ex)
        {
            return Result<TokenPair>.Failure(Error.FromException(ex).Message);
        }
    }
}
