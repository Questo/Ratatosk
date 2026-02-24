using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Identity.Events;

namespace Ratatosk.Application.Authentication.Models;

public sealed record UserAuth(string Email, string Role, string Hash);

public class UserAuthProjection(IUserAuthRepository repo) : IDomainEventHandler<UserCreated>
{
    public async Task WhenAsync(
        UserCreated domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var readModel = new UserAuth(
            domainEvent.Email.Value,
            domainEvent.Role.Id.ToString(),
            domainEvent.PasswordHash.Value
        );

        await repo.SaveAsync(readModel, cancellationToken);
    }
}
