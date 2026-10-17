using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Identity.Events;

public sealed class UserCreated(
    Guid userId,
    Email email,
    UserRole role,
    PasswordHash passwordHash
) : DomainEvent
{
    // Guid.Empty for users created before the id was recorded.
    public Guid UserId { get; } = userId;
    public Email Email { get; } = email;
    public UserRole Role { get; } = role;
    public PasswordHash PasswordHash { get; } = passwordHash;
}

