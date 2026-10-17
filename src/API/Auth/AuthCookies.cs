using Ratatosk.Application.Authentication.Models;

namespace Ratatosk.API.Auth;

/// <summary>
/// The httpOnly cookies that carry the tokens for the web app (ADR 0003).
/// The refresh cookie path includes the <c>/api</c> prefix that the same-origin proxy strips.
/// </summary>
public static class AuthCookies
{
    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";

    private const string AccessTokenPath = "/";
    private const string RefreshTokenPath = "/api/auth/refresh";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public static void Append(HttpResponse response, TokenPair tokens, TimeSpan accessTokenLifetime)
    {
        response.Cookies.Append(
            AccessTokenName,
            tokens.AccessToken,
            Options(AccessTokenPath, DateTimeOffset.UtcNow.Add(accessTokenLifetime))
        );
        response.Cookies.Append(
            RefreshTokenName,
            tokens.RefreshToken,
            Options(RefreshTokenPath, DateTimeOffset.UtcNow.Add(RefreshTokenLifetime))
        );
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenName, Options(AccessTokenPath, expires: null));
        response.Cookies.Delete(RefreshTokenName, Options(RefreshTokenPath, expires: null));
    }

    private static CookieOptions Options(string path, DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = path,
            Expires = expires,
        };
}
