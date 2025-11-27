using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence.ReadModels;
using Ratatosk.Infrastructure.Serialization;
using Ratatosk.Infrastructure.Services;

namespace Ratatosk.Infrastructure.Configuration;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<EventStoreOptions>(
            configuration.GetSection(EventStoreOptions.SectionName)
        );

        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<ISnapshotSerializer, JsonSnapshotSerializer>();

        services.AddScoped<IUnitOfWork>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>();
            var uow = new UnitOfWork(options.Value.ConnectionString);

            // Ensure the connection is opened and a transaction is started
            uow.Begin();

            return uow;
        });

        services.AddScoped<IEventStore>(provider =>
        {
            var eventStoreOptions = provider
                .GetRequiredService<IOptions<EventStoreOptions>>()
                .Value;
            var uow = provider.GetRequiredService<IUnitOfWork>();

            return eventStoreOptions.Type switch
            {
                StoreType.File => new FileEventStore(
                    eventStoreOptions,
                    provider.GetRequiredService<IEventSerializer>()
                ),
                StoreType.Sql => new PostgresEventStore(
                    uow,
                    provider.GetRequiredService<IEventSerializer>()
                ),
                StoreType.InMemory or _ => new InMemoryEventStore(),
            };
        });

        services.AddScoped<ISnapshotStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            var serializer = provider.GetRequiredService<ISnapshotSerializer>();
            var uow = provider.GetRequiredService<IUnitOfWork>();

            return options.Type switch
            {
                StoreType.Sql => new PostgresSnapshotStore(uow, serializer),
                StoreType.InMemory => new InMemorySnapshotStore(),
                _ => throw new NotImplementedException(),
            };
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IProductReadModelRepository, PostgresProductReadModelRepository>();
        services.AddScoped<IProductDomainService, ProductDomainService>();

        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();

        services.AddHostedService<ProjectionRegistrationService>();

        return services;
    }
}
