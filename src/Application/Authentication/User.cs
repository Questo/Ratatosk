namespace Ratatosk.Application.Authentication;

/// <summary>
/// Represents an User that can sign into the application. The User has agency to perform
/// different tasks within the application, and will be restricted to perform certain tasks
/// depending on the user's assigned permissions.
/// </summary>
public class User
{
    public required string Email { get; set; }

    public required UserRole Role { get; set; }
}
