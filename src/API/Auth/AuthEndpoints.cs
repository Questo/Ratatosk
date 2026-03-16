using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;

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
                    IAuthenticationService authService,
                    CancellationToken ct
                ) =>
                {
                    var cmd = new LoginCommand(request.Email, request.Password);
                    var result = await authService.LoginAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    if (!result.IsFailure)
                        return Results.Ok(response);

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
                    IAuthenticationService service,
                    CancellationToken ct
                ) =>
                {
                    var cmd = new SignUpCommand(request.Email, request.Password);
                    var result = await service.SignUpAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .WithTags(AuthTag);

        app.MapPost(
                "/auth/refresh",
                async (
                    RefreshRequest request,
                    IAuthenticationService authService,
                    CancellationToken ct
                ) =>
                {
                    var cmd = new RefreshTokenCommand(request.RefreshToken);
                    var result = await authService.RefreshAsync(cmd, ct);
                    var response = Response<TokenPair>.FromResult(result);

                    return result.IsFailure ? Results.Unauthorized() : Results.Ok(response);
                }
            )
            .WithTags(AuthTag);
    }
}
