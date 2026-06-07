using Fxyoge.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ContextsExample.Game;

public sealed class ContextsStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddContextualScoped<ITrackSession, TrackSession>("track:grand-prix");
        services.AddContextualScoped<ITrackSurface, ForestTrackSurface>("track:grand-prix");

        services.AddContextualScoped<IInputSource, PlayerOneInputSource>("mode:runtime", "player:1");
        services.AddContextualScoped<IInputSource, RivalInputSource>("mode:runtime", "rival:*");
        services.AddContextualScoped<IInputSource, ReplayInputSource>("mode:replay", "recording:*");
        services.AddContextualScoped<IInputSource, PreviewSpinInputSource>("mode:preview", "vehicle:*");

        services.AddContextualScoped<IRunTelemetry, PlayerRunTelemetry>("track:grand-prix", "entrant:*");
        services.AddContextualScoped<IRunTelemetry, ReplayRunTelemetry>("mode:replay", "recording:*");
        services.AddContextualScoped<IRunTelemetry, PreviewRunTelemetry>("mode:preview", "vehicle:*");

        services.AddSingleton<ICarModifier, BaseHandlingModifier>();
        services.AddContextualScoped<ICarModifier, GrandPrixSurfaceModifier>("track:grand-prix");
        services.AddContextualScoped<ICarModifier, PlayerPaintModifier>("player:1");
        services.AddContextualScoped<ICarModifier, RivalPaintModifier>("rival:*");
        services.AddContextualScoped<ICarModifier, ReplayGhostModifier>("mode:replay");
        services.AddContextualScoped<ICarModifier, PreviewShowroomModifier>("mode:preview", "vehicle:*");

        services.AddContextualScoped<ICameraRig, RuntimeCameraRig>("mode:runtime", "camera:chase");
        services.AddContextualScoped<ICameraRig, ReplayCameraRig>("mode:replay", "camera:broadcast");
        services.AddContextualScoped<ICameraRig, PreviewCameraRig>("mode:preview", "camera:garage");

        services.AddTransient<VehicleController>();
        services.AddTransient<VehicleHudModel>();
    }
}
