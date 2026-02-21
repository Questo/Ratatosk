using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;

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
                    var response = Response<string>.FromResult(result);

                    return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
                }
            )
            .WithTags(AuthTag);
    }
}
