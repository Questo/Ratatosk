
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Application.Authentication;

public class UserRole(int id, string name) : Enumeration(id, name)
{
    public static UserRole Admin = new(0, nameof(Admin));
    public static UserRole Merchant = new(1, nameof(Merchant));
    public static UserRole Customer = new(2, nameof(Customer));
}
