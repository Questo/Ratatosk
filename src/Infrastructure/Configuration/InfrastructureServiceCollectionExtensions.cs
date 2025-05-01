using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.Infrastructure.Configuration;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IEventStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var serializer = provider.GetRequiredService<IEventSerializer>();

            return options.Type switch
            {
                EventStoreType.File => new FileEventStore(options, serializer),
                EventStoreType.InMemory or _ => new InMemoryEventStore()
            };
        });
        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));

        return services;
    }
}