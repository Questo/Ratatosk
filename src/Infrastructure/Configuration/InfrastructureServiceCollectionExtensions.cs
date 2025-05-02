using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ratatosk.Application.Catalog.Projections;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;

namespace Ratatosk.Infrastructure.Configuration;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));

        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<ISnapshotSerializer, JsonSnapshotSerializer>();
        services.AddSingleton<IEventStore>(provider =>
        {
            var eventStoreOptions = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();
            var serializer = provider.GetRequiredService<IEventSerializer>();

            return eventStoreOptions.Type switch
            {
                StoreType.File => new FileEventStore(eventStoreOptions, serializer),
                StoreType.Sql => new SqlEventStore(databaseOptions, serializer),
                StoreType.InMemory or _ => new InMemoryEventStore()
            };
        });
        services.AddSingleton<ISnapshotStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();
            var serializer = provider.GetRequiredService<ISnapshotSerializer>();

            return options.Type switch
            {
                StoreType.Sql => new SqlSnapshotStore(databaseOptions, serializer),
                StoreType.InMemory => new InMemorySnapshotStore(),
                _ => throw new NotImplementedException()
            };
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IProductReadModelRepository, SqlProductReadModelRepository>();

        var provider = services.BuildServiceProvider();
        var repo = provider.GetService<IAggregateRepository<Product>>();

        services.AddHostedService<ProjectionRegistrationService>();

        return services;
    }
}