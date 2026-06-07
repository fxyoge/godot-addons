using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public static class ContextualServiceCollectionExtensions
{
    public static IServiceCollection AddContextual<TService, TImplementation>(
        this IServiceCollection services,
        params string[] rules)
        where TService : class
        where TImplementation : class, TService
        => services.AddContextualScoped<TService, TImplementation>(rules);

    public static IServiceCollection AddContextualSingleton<TService, TImplementation>(
        this IServiceCollection services,
        params string[] rules)
        where TService : class
        where TImplementation : class, TService
        => services.AddContextual(
            typeof(TService),
            typeof(TImplementation),
            ServiceLifetime.Singleton,
            rules);

    public static IServiceCollection AddContextualScoped<TService, TImplementation>(
        this IServiceCollection services,
        params string[] rules)
        where TService : class
        where TImplementation : class, TService
        => services.AddContextual(
            typeof(TService),
            typeof(TImplementation),
            ServiceLifetime.Scoped,
            rules);

    public static IServiceCollection AddContextualTransient<TService, TImplementation>(
        this IServiceCollection services,
        params string[] rules)
        where TService : class
        where TImplementation : class, TService
        => services.AddContextual(
            typeof(TService),
            typeof(TImplementation),
            ServiceLifetime.Transient,
            rules);

    private static IServiceCollection AddContextual(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime,
        string[] rules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        var rule = ContextualServiceRule.Parse(rules);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ContextualServiceRegistration)
            && descriptor.ImplementationInstance is ContextualServiceRegistration registration
            && registration.ServiceType == serviceType
            && registration.ImplementationType == implementationType
            && registration.Lifetime == lifetime
            && registration.Rule.ToString() == rule.ToString()))
        {
            return services;
        }

        services.AddSingleton(
            new ContextualServiceRegistration(
                serviceType,
                implementationType,
                lifetime,
                rule));

        return services;
    }
}
