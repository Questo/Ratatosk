using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ratatosk.Core.Abstractions;

namespace Ratatosk.Infrastructure;

public class ProjectionRegistrationService(IServiceProvider serviceProvider, IEventBus eventBus) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe(async (domainEvent, ct) =>
        {
            using var scope = serviceProvider.CreateScope();

            var domainEventType = domainEvent.GetType();
            var projectionType = typeof(IDomainEventHandler<>).MakeGenericType(domainEventType);

            var projections = scope.ServiceProvider.GetServices(projectionType);
            foreach (var projection in projections)
            {
                var whenAsyncMethod = projectionType.GetMethod("WhenAsync");
                if (whenAsyncMethod is null)
                {
                    continue;
                }

                var task = (Task?)whenAsyncMethod.Invoke(projection, [domainEvent, ct]);
                if (task is null)
                {
                    continue;
                }

                await task;
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}