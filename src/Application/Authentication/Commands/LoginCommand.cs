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
        var passwordResult = Password.Create(request.Password);
        if (passwordResult.IsFailure)
        {
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var userAuth = await userAuthRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (userAuth is null)
        {
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var hashToVerify = PasswordHash.Create(userAuth.Hash).Value!;
        var isPasswordValid = passwordHasher.Verify(passwordResult.Value!, hashToVerify);

        if (!isPasswordValid)
        {
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var token = tokenIssuer.IssueToken(userAuth.Email, userAuth.Role, userAuth.Hash);
        if (token.IsFailure)
        {
            return Result<string>.Failure("Could not issue token.");
        }

        return Result<string>.Success(token.Value!);
    }
}
