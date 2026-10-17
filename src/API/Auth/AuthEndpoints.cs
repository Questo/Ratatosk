using Microsoft.Extensions.Options;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.API.Auth;

public static class AuthEndpoints
{
    private const string AuthTag = "Auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/login",
                async (
                    LoginRequest request,
                    IDispatcher dispatcher,
                    IOptions<AuthOptions> authOptions,
                    HttpContext context,
                    CancellationToken ct
                ) =>
                {
                    var cmd = new LoginCommand(request.Email, request.Password);
                    var result = await dispatcher.DispatchAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    if (!result.IsFailure)
                    {
                        AuthCookies.Append(context.Response, result.Value!, AccessTokenLifetime(authOptions));
                        return Results.Ok(response);
                    }

                    return result.Error == Errors.Authentication.InvalidCredentials.Message
                        ? Results.Unauthorized()
                        : Results.BadRequest(response);
                }
            )
            .WithTags(AuthTag);

        app.MapPost(
                "/auth/sign-up",
                async (
                    SignUpRequest request,
                    IDispatcher dispatcher,
                    CancellationToken ct
                ) =>
                {
                    var cmd = new SignUpCommand(request.Email, request.Password);
                    var result = await dispatcher.DispatchAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .WithTags(AuthTag);

        app.MapPost(
                "/auth/refresh",
                async (
                    RefreshRequest? request,
                    IDispatcher dispatcher,
                    IOptions<AuthOptions> authOptions,
                    HttpContext context,
                    CancellationToken ct
                ) =>
                {
                    var refreshToken =
                        request?.RefreshToken ?? context.Request.Cookies[AuthCookies.RefreshTokenName];
                    if (string.IsNullOrEmpty(refreshToken))
                        return Results.Unauthorized();

                    var cmd = new RefreshTokenCommand(refreshToken);
                    var result = await dispatcher.DispatchAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    if (result.IsFailure)
                        return Results.Unauthorized();

                    AuthCookies.Append(context.Response, result.Value!, AccessTokenLifetime(authOptions));
                    return Results.Ok(response);
                }
            )
            .WithTags(AuthTag);

        app.MapPost(
                "/auth/logout",
                (HttpContext context) =>
                {
                    AuthCookies.Delete(context.Response);
                    return Results.NoContent();
                }
            )
            .WithTags(AuthTag);
    }

    private static TimeSpan AccessTokenLifetime(IOptions<AuthOptions> authOptions) =>
        TimeSpan.FromMinutes(authOptions.Value.ExpiresInMinutes);
}
