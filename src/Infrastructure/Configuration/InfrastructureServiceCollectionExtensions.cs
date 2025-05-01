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
        services.AddSingleton<ISnapshotSerializer, JsonSnapshotSerializer>();
        services.AddSingleton<IEventStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var serializer = provider.GetRequiredService<IEventSerializer>();

            return options.Type switch
            {
                StoreType.File => new FileEventStore(options, serializer),
                StoreType.Sql => new SqlEventStore(options, serializer),
                StoreType.InMemory or _ => new InMemoryEventStore()
            };
        });
        services.AddSingleton<ISnapshotStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var serializer = provider.GetRequiredService<ISnapshotSerializer>();

            return options.Type switch
            {
                StoreType.Sql => new SqlSnapshotStore(options, serializer),
                StoreType.InMemory => new InMemorySnapshotStore(),
                _ => throw new NotImplementedException()
            };
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));

        return services;
    }
}