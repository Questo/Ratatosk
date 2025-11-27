using Ratatosk.Application.Authentication;

namespace Ratatosk.API.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/auth/login",
            async (LoginRequest req, IAuthenticationService authService, CancellationToken ct) =>
            {
                var result = await authService.LoginAsync(req.Username, req.Password, ct);

                return result.IsFailure
                    ? Results.BadRequest(result.Error)
                    : Results.Ok(new { Token = result.Value });
            }
        );
    }
}
