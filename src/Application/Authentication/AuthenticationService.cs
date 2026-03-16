using Microsoft.Extensions.Logging;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Authentication;

public interface IAuthenticationService
{
    Task<Result<TokenPair>> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default
    );

    Task<Result<string>> SignUpAsync(
        SignUpCommand command,
        CancellationToken cancellationToken = default
    );

    Task<Result<TokenPair>> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default
    );
}

public sealed class AuthenticationService(
    IDispatcher dispatcher,
    ILogger<AuthenticationService> logger
) : IAuthenticationService
{
    public async Task<Result<TokenPair>> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to login: {Error}", result.Error);

        return result;
    }

    public async Task<Result<string>> SignUpAsync(
        SignUpCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to sign up: {Error}", result.Error);

        return result;
    }

    public async Task<Result<TokenPair>> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await dispatcher.DispatchAsync(command, cancellationToken);
        if (result.IsFailure)
            logger.LogError("Failed to refresh token: {Error}", result.Error);

        return result;
    }
}
