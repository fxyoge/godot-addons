using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fxyoge.DependencyInjection.Configuration;

public static partial class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettings<TOptions>(
        this IServiceCollection services,
        string section,
        Action<SettingsMappingBuilder<TOptions>> configure)
        where TOptions : class, new()
    {
        var mappingBuilder = new SettingsMappingBuilder<TOptions>(section);
        configure(mappingBuilder);

        services.AddSingleton<ISettingsRegistration<TOptions>>(
            new SettingsRegistration<TOptions>(section, mappingBuilder.Build()));
        services.TryAddSingleton<ISettingsMonitor<TOptions>, SettingsMonitor<TOptions>>();
        services.TryAddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<TOptions>>(
            provider => provider.GetRequiredService<ISettingsMonitor<TOptions>>());

        return services;
    }

    public static IServiceCollection AddUserConfig(
        this IServiceCollection services,
        string overlayPath = "user://settings.cfg")
    {
        services.TryAddSingleton<IConfigOverlayStore>(_ => new GodotConfigFileOverlayStore(overlayPath));
        return services;
    }
}
