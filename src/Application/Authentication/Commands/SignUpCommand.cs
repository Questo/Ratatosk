using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication.Commands;

public sealed record SignUpCommand(string Email, string Password) : IRequest<Result<string>>;

public sealed class SignUpCommandHandler(
    IAggregateRepository<User> userRepository,
    IEventBus eventBus,
    IUserAuthRepository userAuthRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer
) : IRequestHandler<SignUpCommand, Result<string>>
{
    public async Task<Result<string>> HandleAsync(
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
            {
                return Result<string>.Failure($"User is not null: {userAuth.Email}");
            }

            var passwordResult = Password.Create(request.Password);
            if (passwordResult.IsFailure)
            {
                return Result<string>.Failure(passwordResult.Error!);
            }

            var passwordHash = passwordHasher.Hash(passwordResult.Value!);

            var user = User.Create(request.Email, UserRole.User, passwordHash.Value!);
            await userRepository.SaveAsync(user, cancellationToken);

            foreach (var domainEvent in user.UncommittedEvents)
                await eventBus.PublishAsync(domainEvent, cancellationToken);

            var tokenResult = tokenIssuer.IssueToken(user.Email.Value, user.Role.Name);
            if (tokenResult.IsFailure)
            {
                return Result<string>.Failure("Could not issue token.");
            }

            return Result<string>.Success(tokenResult.Value!);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(Error.FromException(ex).Message);
        }
    }
}
