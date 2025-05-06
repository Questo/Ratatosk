namespace Ratatosk.Infrastructure.Configuration;

public class AuthOptions
{
    public const string SectionName = "Authentication";

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int ExpiresInMinutes { get; set; } = 60;
}
