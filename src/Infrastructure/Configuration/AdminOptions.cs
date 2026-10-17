namespace Ratatosk.Infrastructure.Configuration;

/// <summary>The Admin User seeded at startup. Seeding is skipped when either value is blank.</summary>
public class AdminOptions
{
    public const string SectionName = "Admin";

    public string? Email { get; set; }
    public string? Password { get; set; }
}
