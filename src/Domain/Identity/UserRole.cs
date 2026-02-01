using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Domain.Identity;

public class UserRole(int id, string name) : Enumeration(id, name)
{
    public static UserRole Admin = new(0, nameof(Admin));
    public static UserRole User = new(1, nameof(User));
}
