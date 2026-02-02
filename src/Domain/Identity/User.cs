using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity.Events;

namespace Ratatosk.Domain.Identity;

/// <summary>
/// Represents an User that can sign into the application. The User has agency to perform
/// different tasks within the application, and will be restricted to perform certain tasks
/// depending on the user's assigned permissions.
/// </summary>
public sealed class User : AggregateRoot
{
    public Email Email { get; private set; } = default!;

    public UserRole Role { get; private set; } = UserRole.User;

    public UserProfile Profile { get; private set; } = default!;

    public PasswordHash PasswordHash { get; private set; } = default!;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case UserCreated userCreated:
                Email = userCreated.Email;
                Role = userCreated.Role;
                PasswordHash = userCreated.PasswordHash;
                Profile = new UserProfile() { Name = userCreated.Email.Value };
                break;

            case UserProfileUpdated profileUpdated:
                Profile = profileUpdated.Profile;
                break;

            default:
                throw new NotImplementedException(
                    $"Event {domainEvent.GetType().Name} is not supported."
                );
        }
    }

    public static User Create(string email, UserRole role, string passwordHash)
    {
        var emailResult = Email.Create(email);
        if (emailResult.IsFailure)
            throw new ArgumentException(emailResult.Error, nameof(email));

        var hashResult = PasswordHash.Create(passwordHash);
        if (hashResult.IsFailure)
            throw new ArgumentException(hashResult.Error, nameof(passwordHash));

        var user = new User();

        user.RaiseEvent(new UserCreated(emailResult.Value!, role, hashResult.Value!));

        return user;
    }

    public void UpdateProfile(UserProfile profile)
    {
        Guard.AgainstNull(profile, nameof(profile));

        RaiseEvent(new UserProfileUpdated(profile));
    }
}
