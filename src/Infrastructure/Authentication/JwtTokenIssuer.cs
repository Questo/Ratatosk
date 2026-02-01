using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.Application.Authentication;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Authentication;

public sealed class JwtTokenIssuer(IOptions<AuthOptions> options) : ITokenIssuer
{
    public Result<string> IssueToken(User user)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, user.Profile.Name), new Claim("role", "Merchant") };

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
        return Result<string>.Success(tokenString);
    }
}