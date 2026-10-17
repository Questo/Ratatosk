using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.Infrastructure;

/// <summary>
/// Delivers every published event to its <see cref="IDomainEventHandler{TEvent}"/>s. Each handler
/// runs in its own DI scope and transaction: it commits on success, and on failure it is rolled
/// back and logged without affecting the publisher (whose events are already committed) or the
/// event's other handlers.
/// </summary>
public class ProjectionRegistrationService(
    IServiceProvider serviceProvider,
    IEventBus eventBus,
    ILogger<ProjectionRegistrationService> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe(DispatchAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var whenAsync = handlerType.GetMethod(nameof(IDomainEventHandler<DomainEvent>.WhenAsync))!;

        int? handlerCount = null;
        for (var i = 0; handlerCount is null || i < handlerCount; i++)
        {
            using var scope = serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();
            handlerCount ??= handlers.Count;
            if (i >= handlers.Count)
                break;

            var handler = handlers[i]!;
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            try
            {
                await InvokeAsync(whenAsync, handler, domainEvent, cancellationToken);
                uow.Commit();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TryRollback(uow);
                logger.LogError(
                    ex,
                    "{Handler} failed to handle {Event} (version {Version})",
                    handler.GetType().Name,
                    domainEvent.GetType().Name,
                    domainEvent.Version
                );
            }
        }
    }

    // Reflection wraps a synchronous throw in TargetInvocationException; surface the real one.
    private static Task InvokeAsync(
        MethodInfo whenAsync,
        object handler,
        DomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return (Task)whenAsync.Invoke(handler, [domainEvent, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    // The scope is being thrown away; a failed rollback must not hide the handler's own error.
    private static void TryRollback(IUnitOfWork uow)
    {
        try
        {
            uow.Rollback();
        }
        catch { }
    }
}
