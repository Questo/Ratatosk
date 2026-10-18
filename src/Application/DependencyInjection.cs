using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Changes;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Search;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<StockKeeper>();

        services.AddRequestHandlers();
        services.AddProjections();
        services.AddSearchProviders();

        return services;
    }

    public static IServiceCollection AddRequestHandlers(this IServiceCollection services)
    {
        services.AddImplementationsOfOpenGeneric(
            typeof(IRequestHandler<,>),
            typeof(AddProductCommandHandler).Assembly
        );
        return services;
    }

    private static IServiceCollection AddProjections(this IServiceCollection services)
    {
        services.AddImplementationsOfOpenGeneric(
            typeof(IDomainEventHandler<>),
            typeof(ProductProjection).Assembly,
            excluded: typeof(ChangeSignalHandler)
        );

        // The dispatcher runs an event's handlers in registration order, committing each before
        // the next. Registering ChangeSignalHandler last means a change signal only goes out once
        // the read model it points to is written, so a client refetching on the signal sees it.
        services.AddImplementationsOfOpenGeneric(
            typeof(IDomainEventHandler<>),
            typeof(ChangeSignalHandler)
        );
        return services;
    }

    private static IServiceCollection AddSearchProviders(this IServiceCollection services)
    {
        var providerTypes = typeof(ISearchProvider)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISearchProvider).IsAssignableFrom(t));

        foreach (var providerType in providerTypes)
        {
            services.AddScoped(typeof(ISearchProvider), providerType);
        }

        return services;
    }

    private static IServiceCollection AddImplementationsOfOpenGeneric(
        this IServiceCollection services,
        Type openGenericInterface,
        Assembly targetAssembly,
        Type? excluded = null
    )
    {
        var typesToRegister = targetAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && t != excluded)
            .ToList();

        return services.AddImplementationsOfOpenGeneric(openGenericInterface, typesToRegister);
    }

    private static IServiceCollection AddImplementationsOfOpenGeneric(
        this IServiceCollection services,
        Type openGenericInterface,
        params IEnumerable<Type> typesToRegister
    )
    {
        foreach (var implementationType in typesToRegister)
        {
            var interfaces = implementationType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface
                );

            foreach (var interfaceType in interfaces)
            {
                services.AddScoped(interfaceType, implementationType);
            }
        }

        return services;
    }
}
