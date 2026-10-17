using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.Application.Authentication;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Authentication;

public sealed class JwtTokenIssuer(IOptions<AuthOptions> options) : ITokenIssuer
{
    public Result<string> IssueToken(Guid userId, string email, string role)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email), new(ClaimTypes.Role, role) };

        // Users created before ids were recorded have none to put in the token.
        if (userId != Guid.Empty)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var key = new SymmetricSecurityKey(options.Value.GetKeyBytes());
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.Value.ExpiresInMinutes),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Result<string>.Success(tokenString);
    }
}
