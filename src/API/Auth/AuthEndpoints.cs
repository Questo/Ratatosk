using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;

namespace Ratatosk.API.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/auth/login",
            async (LoginCommand cmd, IAuthenticationService authService, CancellationToken ct) =>
            {
                var result = await authService.LoginAsync(cmd, ct);
                var response = Response<string>.FromResult(result);

                return result.IsFailure ? Results.BadRequest(response) : Results.Ok(response);
            }
        );
    }
}
