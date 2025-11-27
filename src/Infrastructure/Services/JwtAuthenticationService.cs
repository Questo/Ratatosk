using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.Application.Authentication;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Services;

public class JwtAuthenticationService(IOptions<AuthOptions> options) : IAuthenticationService
{
    public Task<Result<string>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        // Replace with real validation logic
        if (username != "ratatosk" || password != "ratatest123")
        {
            return Task.FromResult(Result<string>.Failure("Invalid credentials"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, username), new Claim("role", "Merchant") };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.Value.ExpiresInMinutes),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult(Result<string>.Success(tokenString));
    }
}
