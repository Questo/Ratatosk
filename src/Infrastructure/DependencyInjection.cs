using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Ordering;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.Authentication;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence.ReadModels;
using Ratatosk.Infrastructure.Persistence.Repositories;
using Ratatosk.Infrastructure.Serialization.Serializers;
using Ratatosk.Infrastructure.Services;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure;

public static class DependencyInjection
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

        // The in-memory stores *are* the storage, so one instance must outlive every scope.
        services.AddSingleton<InMemoryEventStore>();
        services.AddSingleton<InMemorySnapshotStore>();

        services.AddScoped<IEventStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;

            return options.Type switch
            {
                StoreType.File => new FileEventStore(
                    options,
                    provider.GetRequiredService<IEventSerializer>()
                ),
                StoreType.Sql => new PostgresEventStore(
                    provider.GetRequiredService<IUnitOfWork>(),
                    provider.GetRequiredService<IEventSerializer>()
                ),
                StoreType.InMemory or _ => provider.GetRequiredService<InMemoryEventStore>(),
            };
        });

        services.AddScoped<ISnapshotStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;

            return options.Type switch
            {
                StoreType.File => new FileSnapshotStore(
                    options,
                    provider.GetRequiredService<ISnapshotSerializer>()
                ),
                StoreType.Sql => new PostgresSnapshotStore(
                    provider.GetRequiredService<IUnitOfWork>(),
                    provider.GetRequiredService<ISnapshotSerializer>()
                ),
                StoreType.InMemory or _ => provider.GetRequiredService<InMemorySnapshotStore>(),
            };
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IProductReadModelRepository, ProductReadModelRepository>();
        services.AddScoped<IProductDomainService, ProductDomainService>();

        services.AddScoped<IInventoryReadModelRepository, InventoryReadModelRepository>();

        services.AddScoped<IOrderReadModelRepository, OrderReadModelRepository>();

        services.AddScoped<IUserAuthRepository, UserAuthReadModel>();

        services.AddScoped<IRefreshTokenRepository>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;
            return options.Type switch
            {
                StoreType.Sql => new PostgresRefreshTokenRepository(
                    provider.GetRequiredService<IUnitOfWork>()
                ),
                _ => new InMemoryRefreshTokenRepository(),
            };
        });

        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();

        services.AddHostedService<EventHandlerDispatcher>();

        return services;
    }
}
