using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Identity.Events;

public sealed class UserCreated(Email email, UserRole role, PasswordHash passwordHash)
    : DomainEvent
{
    public Email Email { get; } = email;
    public UserRole Role { get; } = role;
    public PasswordHash PasswordHash { get; } = passwordHash;
}

