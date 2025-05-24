using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Services;

namespace Ratatosk.Infrastructure.Configuration;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
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

            var connection = new NpgsqlConnection(databaseOptions.Value.ConnectionString);
            connection.Open();

            var transaction = connection.BeginTransaction();

            return eventStoreOptions.Type switch
            {
                StoreType.File => new FileEventStore(eventStoreOptions, serializer),
                StoreType.Sql => new PostgresEventStore(connection, transaction, serializer),
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
                StoreType.Sql => new PostgresSnapshotStore(databaseOptions, serializer),
                StoreType.InMemory => new InMemorySnapshotStore(),
                _ => throw new NotImplementedException()
            };
        });

        services.AddScoped<IDbConnection>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>();
            var connection = new NpgsqlConnection(options.Value.ConnectionString);
            connection.Open();
            return connection;
        });

        services.AddScoped<IDbTransaction>(provider =>
        {
            var connection = provider.GetRequiredService<IDbConnection>();
            return connection.BeginTransaction();
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IProductReadModelRepository, PostgresProductReadModelRepository>();
        services.AddScoped<IProductDomainService, ProductDomainService>();

        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();

        services.AddHostedService<ProjectionRegistrationService>();

        return services;
    }
}
