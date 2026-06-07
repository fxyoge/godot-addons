using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigExample.Game;

public sealed class ConfigStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddFxyDiConfig();

        services.AddWritableOptions<AudioOptions>("audio", audio =>
        {
            audio.Map(x => x.MasterVolume)
                .WithUi("Master Volume", ConfigUiControl.Slider, 0, 1, 0.01)
                .ToAudioBusVolume("Master", 0.8f);

            audio.Map(x => x.Muted)
                .WithUi("Mute", ConfigUiControl.Toggle)
                .ToAudioBusMute("Master", false);
        });

        services.AddWritableOptions<InputOptions>("input", input =>
        {
            input.Map(x => x.Jump)
                .WithUi("Jump", ConfigUiControl.KeyBinding)
                .ToInputAction("jump", Key.Space);
        });

        services.AddWritableOptions<GameplayOptions>("gameplay", gameplay =>
        {
            gameplay.Map(x => x.Difficulty)
                .WithUi("Difficulty", ConfigUiControl.Select)
                .ToUserConfig("Normal");

            gameplay.Map(x => x.ShowDamageNumbers)
                .WithUi("Show Damage Numbers", ConfigUiControl.Toggle)
                .ToUserConfig(true);

            gameplay.Map(x => x.CameraSensitivity)
                .WithUi("Camera Sensitivity", ConfigUiControl.Slider, 0.1, 2.0, 0.05)
                .ToUserConfig(1.0f);
        });

        services.AddWritableOptions<ProjectDefaultsOptions>("project", project =>
        {
            project.Map(x => x.GameTitle)
                .WithUi("Game Title", ConfigUiControl.Text)
                .ToProjectSettingDefault("application/config/name", "Fxy DI Config Example");
        });
    }
}
