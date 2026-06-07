using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigExample.Game;

public sealed class ConfigStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddUserConfig();

        services.AddSettings<AudioOptions>("audio", audio =>
        {
            audio.Map(x => x.MasterVolume)
                .WithUi("Master Volume", ConfigUiControl.Slider, 0, 1, 0.01)
                .PersistAs("master/volume")
                .ToRuntime(new GodotAudioBusVolumeBinding("Master", 0.8f), 0.8f);

            audio.Map(x => x.Muted)
                .WithUi("Mute", ConfigUiControl.Toggle)
                .PersistAs("master/muted")
                .ToRuntime(new GodotAudioBusMuteBinding("Master", false), false);
        });

        services.AddSettings<InputOptions>("input", input =>
        {
            input.Map(x => x.Jump)
                .WithUi("Jump", ConfigUiControl.KeyBinding)
                .PersistAs("jump")
                .ToRuntime(
                    new GodotInputActionBinding("jump", (long)Key.Space),
                    InputActionBinding.FromKeyCode((long)Key.Space, keyCode => OS.GetKeycodeString((Key)keyCode)),
                    new InputActionBindingConfigValueCodec(keyCode => OS.GetKeycodeString((Key)keyCode)));
        });

        services.AddSettings<GameplayOptions>("gameplay", gameplay =>
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

        services.AddSettings<ProjectDefaultsOptions>("project", project =>
        {
            project.Map(x => x.GameTitle)
                .WithUi("Game Title", ConfigUiControl.Text)
                .PersistAs("application/config/name")
                .ToRuntime(
                    new GodotProjectSettingBinding<string>(
                        "application/config/name",
                        "DI Config Example",
                        runtimeMutable: false,
                        requiresRestart: true),
                    "DI Config Example");
        });
    }
}
