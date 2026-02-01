using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Identity.Events;

public sealed class UserCreated(string email, UserRole role, PasswordHash passwordHash)
    : DomainEvent
{
    public string Email { get; } = email;
    public UserRole Role { get; } = role;
    public PasswordHash PasswordHash { get; } = passwordHash;
}

