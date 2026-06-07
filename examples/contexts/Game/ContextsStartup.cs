using Fxyoge.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ContextsExample.Game;

public sealed class ContextsStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
        services.AddSingleton<ISettingsStore, UserSettingsStore>();
        services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        services.AddContextualScoped<ISettingsStore, ReplaySettingsStore>("replay");
        services.AddTransient<SettingsManager>();

        services.AddContextualScoped<ILevelSession, LevelSession>("level:*");
        services.AddContextualScoped<IPlayerProfile, PlayerProfile>("level:*", "player:*");

        services.AddContextualScoped<IControlMode, RuntimeControlMode>("runtime");
        services.AddContextualScoped<IControlMode, PreviewControlMode>("preview");
        services.AddContextualScoped<IControlMode, ReplayControlMode>("replay");

        services.AddContextualScoped<IDebugOverlayModel, DebugOverlayModel>("preview");
        services.AddContextualScoped<ICameraRig, PreviewCameraRig>("preview", "level:*");
    }
}
