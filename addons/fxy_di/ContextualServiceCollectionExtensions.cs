using System;
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

        services.AddSingleton(
            new ContextualServiceRegistration(
                serviceType,
                implementationType,
                lifetime,
                ContextualServiceRule.Parse(rules)));

        return services;
    }
}
