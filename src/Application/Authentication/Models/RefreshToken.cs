namespace Ratatosk.Application.Authentication.Models;

public sealed record RefreshToken(string Token, string Email, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
}
