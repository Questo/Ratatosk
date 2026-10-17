using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

/// <summary>
/// Creates the configured Admin User, unless a User with that email already exists (whatever
/// its Role), in which case it is left as is.
/// </summary>
public sealed record SeedAdminCommand(string Email, string Password) : IRequest<Result>;

public sealed class SeedAdminCommandHandler(
    IAggregateRepository<User> userRepository,
    IUserAuthRepository userAuthRepository,
    IPasswordHasher passwordHasher
) : IRequestHandler<SeedAdminCommand, Result>
{
    public async Task<Result> HandleAsync(
        SeedAdminCommand request,
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
                return Result.Success();

            var passwordResult = Password.Create(request.Password);
            if (passwordResult.IsFailure)
                return Result.Failure(passwordResult.Error!);

            var passwordHash = passwordHasher.Hash(passwordResult.Value!);

            var user = User.Create(request.Email, UserRole.Admin, passwordHash.Value!);
            await userRepository.CommitAsync(user, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.FromException(ex).Message);
        }
    }
}
