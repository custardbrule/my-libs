using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace CustardBruleMediatr;
public static class DependencyInjection
{
    /// <summary>
    /// Register all CQRS components from the specified assemblies
    /// </summary>
    public static IServiceCollection AddCQRS(this IServiceCollection services, ServiceLifetime handlerLifeTime, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        return services
            .AddCQRSCore()
            .AddHandlers(handlerLifeTime, assemblies);
    }

    /// <summary>
    /// Register all CQRS components from the specified types
    /// </summary>
    public static IServiceCollection AddCQRS(this IServiceCollection services, ServiceLifetime handlerLifeTime, params Type[] markerTypes)
    {
        var assemblies = markerTypes.Select(t => t.Assembly).Distinct().ToArray();
        return services.AddCQRS(handlerLifeTime, assemblies);
    }

    /// <summary>
    /// Register core CQRS services
    /// </summary>
    public static IServiceCollection AddCQRSCore(this IServiceCollection services)
    {
        // Register the publisher
        services.TryAddScoped<IPublisher, Publisher>();

        return services;
    }

    /// <summary>
    /// Register all handlers from the specified assemblies
    /// </summary>
    public static IServiceCollection AddHandlers(this IServiceCollection services, ServiceLifetime handlerLifeTime, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        foreach (var assembly in assemblies)
        {
            RegisterHandlers(services, handlerLifeTime, assembly);
        }

        return services;
    }

    /// <summary>
    /// Register a specific handler
    /// </summary>
    public static IServiceCollection AddHandler<THandler>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where THandler : class
    {
        var handlerType = typeof(THandler);
        var interfaces = GetHandlerInterfaces(handlerType);

        foreach (var interfaceType in interfaces)
            services.Add(new ServiceDescriptor(interfaceType, handlerType, lifetime));

        return services;
    }

    /// <summary>
    /// Register a specific handler with custom factory
    /// </summary>
    public static IServiceCollection AddHandler<THandler>(this IServiceCollection services,
        Func<IServiceProvider, THandler> factory,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where THandler : class
    {
        var handlerType = typeof(THandler);
        var interfaces = GetHandlerInterfaces(handlerType);

        foreach (var interfaceType in interfaces)
            services.Add(new ServiceDescriptor(interfaceType, sp => factory(sp), lifetime));

        return services;
    }

    /// <summary>
    /// Conditional registration - only register if not already registered
    /// </summary>
    public static IServiceCollection TryAddHandler<THandler>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where THandler : class
    {
        var handlerType = typeof(THandler);
        var interfaces = GetHandlerInterfaces(handlerType);

        foreach (var interfaceType in interfaces)
            services.TryAdd(new ServiceDescriptor(interfaceType, handlerType, lifetime));

        return services;
    }

    /// <summary>
    /// Register handlers with validation
    /// </summary>
    public static IServiceCollection AddValidatedHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        var handlerTypes = new List<Type>();
        var requestTypes = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .ToArray();

            // Collect handler types and their request types
            foreach (var type in types)
            {
                var interfaces = GetHandlerInterfaces(type);
                if (interfaces.Any())
                {
                    handlerTypes.Add(type);
                    foreach (var interfaceType in interfaces)
                    {
                        var requestType = interfaceType.GetGenericArguments()[0];
                        requestTypes.Add(requestType);
                    }
                }
            }
        }

        // Validate no duplicate handlers for same request
        var duplicates = handlerTypes
            .SelectMany(ht => GetHandlerInterfaces(ht).Select(i => new { HandlerType = ht, RequestType = i.GetGenericArguments()[0] }))
            .GroupBy(x => x.RequestType)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Any())
        {
            var duplicateInfo = string.Join(", ", duplicates.Select(d =>
                $"{d.Key.Name}: [{string.Join(", ", d.Select(h => h.HandlerType.Name))}]"));
            throw new InvalidOperationException($"Multiple handlers found for requests: {duplicateInfo}");
        }

        // Register all handlers
        foreach (var handlerType in handlerTypes)
        {
            var interfaces = GetHandlerInterfaces(handlerType);
            foreach (var interfaceType in interfaces)
            {
                services.AddScoped(interfaceType, handlerType);
            }
        }

        return services;
    }

    #region Private Helper Methods
    private static void RegisterHandlers(IServiceCollection services, ServiceLifetime handlerLifeTime, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => GetHandlerInterfaces(t).Any())
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = GetHandlerInterfaces(handlerType);
            foreach (var interfaceType in interfaces)
            {
                services.Add(new ServiceDescriptor(interfaceType, handlerType, handlerLifeTime));
            }
        }
    }

    private static IEnumerable<Type> GetHandlerInterfaces(Type type)
    {
        return type.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i =>
            {
                var genericTypeDefinition = i.GetGenericTypeDefinition();
                return genericTypeDefinition == typeof(IHandler<,>) ||
                       genericTypeDefinition == typeof(IHandler<>);
            });
    }
    #endregion
}
