using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.API.Configuration;

public static class APIServiceCollectionExtensions
{
    public static IServiceCollection AddAPI(this IServiceCollection services, IConfiguration configuration)
    {
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        services.AddOpenApi();

        services.AddControllers();

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();
        Guard.AgainstNull(authOptions, nameof(authOptions));
        var key = authOptions!.GetKeyBytes();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Issuer,

                    ValidateAudience = false,
                    ValidAudience = authOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });
        services.AddAuthorization();

        return services;
    }
}
