using System.Security.Cryptography;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record SignUpCommand(string Email, string Password) : IRequest<Result<TokenPair>>;

public sealed class SignUpCommandHandler(
    IAggregateRepository<User> userRepository,
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
            // Stored emails are normalized by Email.Create; look up the same form.
            var emailResult = Email.Create(request.Email);
            if (emailResult.IsFailure)
                return Result<TokenPair>.Failure(emailResult.Error!);

            var userAuth = await userAuthRepository.GetByEmailAsync(
                emailResult.Value!.Value,
                cancellationToken
            );
            if (userAuth is not null)
                return Result<TokenPair>.Failure(Errors.Authentication.AccountAlreadyExists.Message);

            var passwordResult = Password.Create(request.Password);
            if (passwordResult.IsFailure)
                return Result<TokenPair>.Failure(passwordResult.Error!);

            var passwordHash = passwordHasher.Hash(passwordResult.Value!);

            var user = User.Create(emailResult.Value.Value, UserRole.User, passwordHash.Value!);
            await userRepository.CommitAsync(user, cancellationToken);

            var tokenResult = tokenIssuer.IssueToken(user.Id, user.Email.Value, user.Role.Name);
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
