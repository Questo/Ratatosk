using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.API.Auth;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.API;

public static class DependencyInjection
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

                    ValidateAudience = true,
                    ValidAudience = authOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };

                // The web app sends the JWT in the httpOnly cookie; the Authorization header wins when present.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (!context.Request.Headers.ContainsKey(HeaderNames.Authorization))
                            context.Token = context.Request.Cookies[AuthCookies.AccessTokenName];

                        return Task.CompletedTask;
                    },
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Authenticated, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(Policies.Admin, policy => policy.RequireRole(UserRole.Admin.Name));
        });

        // Registered after AddInfrastructure's EventHandlerDispatcher, so the projections see the Admin.
        services.AddHostedService<AdminSeeder>();

        return services;
    }
}
