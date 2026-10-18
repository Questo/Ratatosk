using Microsoft.Extensions.Options;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.API;

/// <summary>
/// Seeds the configured Admin User at startup. Runs after <c>EventHandlerDispatcher</c> has
/// subscribed, so the User's projection is written and it can log in. A misconfigured Admin
/// stops startup; a missing one is only logged.
/// </summary>
public sealed class AdminSeeder(
    IServiceProvider serviceProvider,
    IOptions<AdminOptions> options,
    ILogger<AdminSeeder> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var admin = options.Value;
        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogInformation("Admin seeding skipped: Admin:Email or Admin:Password is not set");
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var result = await dispatcher.DispatchAsync(
            new SeedAdminCommand(admin.Email, admin.Password),
            cancellationToken
        );
        if (result.IsFailure)
        {
            uow.Rollback();
            throw new InvalidOperationException($"Admin seeding failed: {result.Error}");
        }

        uow.Commit();
        logger.LogInformation("Admin seeding done for {Email}", admin.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
