using System;
using Fxyoge.DependencyInjection;
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
        services.TryAddSingleton<SettingsMonitor<TOptions>>();
        services.TryAddSingleton<ISettingsMonitor<TOptions>>(
            provider => provider.GetRequiredService<SettingsMonitor<TOptions>>());
        services.TryAddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<TOptions>>(
            provider => provider.GetRequiredService<ISettingsMonitor<TOptions>>());
        services.AddContextualScoped<ISettingsMonitor<TOptions>, TransactionalSettingsMonitor<TOptions>>(
            "settings-transaction:*");

        return services;
    }

    public static IServiceCollection AddUserConfig(
        this IServiceCollection services,
        string overlayPath = "user://settings.cfg")
    {
        services.TryAddSingleton<IConfigOverlayStore>(_ => new GodotConfigFileOverlayStore(overlayPath));
        services.AddContextualScoped<ISettingsTransaction, SettingsTransaction>("settings-transaction:*");
        return services;
    }
}
