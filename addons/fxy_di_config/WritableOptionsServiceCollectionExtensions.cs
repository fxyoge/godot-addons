using System;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection.Configuration;

public static class WritableOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddWritableOptions<TOptions>(
        this IServiceCollection services,
        string section,
        Action<WritableOptionsMappingBuilder<TOptions>> configure)
        where TOptions : class, new()
    {
        var mappingBuilder = new WritableOptionsMappingBuilder<TOptions>(section);
        configure(mappingBuilder);

        services.AddSingleton<IWritableOptionsRegistration<TOptions>>(
            new WritableOptionsRegistration<TOptions>(section, mappingBuilder.Build()));
        services.AddSingleton<IWritableOptionsMonitor<TOptions>, WritableOptionsMonitor<TOptions>>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<TOptions>>(
            provider => provider.GetRequiredService<IWritableOptionsMonitor<TOptions>>());

        return services;
    }
}
