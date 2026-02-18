using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<string>>;

public sealed class LoginCommandHandler(
    IAggregateRepository<User> users,
    IUserAuthRepository userSummaries,
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

        var userSummary = await userSummaries.GetUserSummaryAsync(request.Email, cancellationToken);
        if (userSummary is null)
        {
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var user = (await users.LoadAsync(userSummary.Id, cancellationToken)).Value!;

        var hashToVerify = user.PasswordHash;
        var isPasswordValid = passwordHasher.Verify(passwordResult.Value!, hashToVerify);

        if (!isPasswordValid)
        {
            return Result<string>.Failure(Errors.Authentication.InvalidCredentials.Message);
        }

        var token = tokenIssuer.IssueToken(user);
        if (token.IsFailure)
        {
            return Result<string>.Failure("Could not issue token.");
        }

        return Result<string>.Success(token.Value!);
    }
}
