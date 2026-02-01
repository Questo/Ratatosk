using System.Net.Mail;
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
    public MailAddress Email { get; private set; } = default!;

    public UserRole Role { get; private set; } = UserRole.User;

    public UserProfile Profile { get; private set; } = default!;

    public PasswordHash PasswordHash { get; private set; } = default!;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case UserCreated userCreated:
                Email = new MailAddress(userCreated.Email);
                Role = userCreated.Role;
                Profile = new UserProfile() { Name = userCreated.Email };
                PasswordHash = userCreated.PasswordHash;
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

    public static User Create(string email, UserRole role, PasswordHash passwordHash)
    {
        Guard.AgainstNullOrEmpty(email, nameof(email));
        Guard.AgainstNull(role, nameof(role));
        Guard.AgainstNull(passwordHash, nameof(passwordHash));

        MailAddress mailAddress;
        try
        {
            mailAddress = new MailAddress(email);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid email format.", nameof(email));
        }

        var user = new User();

        user.RaiseEvent(new UserCreated(email, role, passwordHash));

        return user;
    }

    public void UpdateProfile(UserProfile profile)
    {
        Guard.AgainstNull(profile, nameof(profile));

        RaiseEvent(new UserProfileUpdated(profile));
    }
}
