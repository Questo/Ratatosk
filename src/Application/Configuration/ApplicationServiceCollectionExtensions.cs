using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;
using Ratatosk.Application.Catalog.Projections;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Application.Configuration;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Dispatcher>();
        services.AddScoped<ICatalogService, CatalogService>();

        services.AddRequestHandlers();
        services.AddProjections();

        return services;
    }

    public static IServiceCollection AddRequestHandlers(this IServiceCollection services)
    {
        services.AddImplementationsOfOpenGeneric(typeof(IRequestHandler<,>), typeof(AddProductCommandHandler).Assembly);
        return services;
    }

    private static IServiceCollection AddProjections(this IServiceCollection services)
    {
        services.AddImplementationsOfOpenGeneric(typeof(IDomainEventHandler<>), typeof(ProductProjection).Assembly);
        return services;
    }

    private static IServiceCollection AddImplementationsOfOpenGeneric(this IServiceCollection services, Type openGenericInterface, Assembly targetAssembly)
    {
        var typesToRegister = targetAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract &&
                        !t.IsInterface &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface))
            .ToList();

        foreach (var implementationType in typesToRegister)
        {
            var interfaceType = implementationType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);

            Console.WriteLine($"Registering {implementationType.Name} as {interfaceType.Name}");
            services.AddScoped(interfaceType, implementationType);
        }

        return services;
    }
}