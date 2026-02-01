using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Identity.Events;

public sealed class UserProfileUpdated(
    UserProfile Profile
) : DomainEvent
{
    public UserProfile Profile { get; } = Profile;
}