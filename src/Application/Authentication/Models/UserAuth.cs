using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Identity.Events;

namespace Ratatosk.Application.Authentication.Models;

// Id is Guid.Empty for users created before the id was recorded.
public sealed record UserAuth(string Email, string Role, string Hash, Guid Id);

public class UserAuthProjection(IUserAuthRepository repo) : IDomainEventHandler<UserCreated>
{
    public async Task WhenAsync(
        UserCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = new UserAuth(
            domainEvent.Email.Value,
            domainEvent.Role.Name,
            domainEvent.PasswordHash.Value,
            domainEvent.UserId
        );

        await repo.SaveAsync(readModel, cancellationToken);
    }
}
