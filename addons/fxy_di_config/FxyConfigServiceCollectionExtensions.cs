using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection.Configuration;

public static class FxyConfigServiceCollectionExtensions
{
    public static IServiceCollection AddFxyDiConfig(
        this IServiceCollection services,
        string overlayPath = "user://settings.cfg")
    {
        services.AddSingleton<IConfigOverlayStore>(_ => new GodotConfigFileOverlayStore(overlayPath));
        return services;
    }
}
