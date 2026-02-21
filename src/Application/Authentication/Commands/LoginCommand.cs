using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<string>>;

public sealed class LoginCommandHandler(
    IUserAuthRepository userAuthRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer
) : IRequestHandler<LoginCommand, Result<string>>
{
    public async Task<Result<string>> HandleAsync(
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
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var tokenResult = tokenIssuer.IssueToken(userAuth.Email, userAuth.Role);
        if (tokenResult.IsFailure)
        {
            return Result<string>.Failure("Could not issue token.");
        }

        return Result<string>.Success(tokenResult.Value!);
    }
}
