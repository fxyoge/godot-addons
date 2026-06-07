using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public static class GodotMappingExtensions
{
    public static void ToInputAction<TOptions>(
        this OptionPropertyMappingBuilder<TOptions, InputActionBinding> builder,
        string actionName,
        Key fallbackKey)
        where TOptions : class, new()
    {
        builder.ToRuntime(new GodotInputActionBinding(actionName, (long)fallbackKey), new InputActionBinding
        {
            KeyCode = (long)fallbackKey,
            DisplayName = OS.GetKeycodeString(fallbackKey),
        });
    }

    public static void ToAudioBusVolume<TOptions>(
        this OptionPropertyMappingBuilder<TOptions, float> builder,
        string busName,
        float fallbackLinearVolume)
        where TOptions : class, new()
    {
        builder.ToRuntime(new GodotAudioBusVolumeBinding(busName, fallbackLinearVolume), fallbackLinearVolume);
    }

    public static void ToAudioBusMute<TOptions>(
        this OptionPropertyMappingBuilder<TOptions, bool> builder,
        string busName,
        bool fallbackMuted)
        where TOptions : class, new()
    {
        builder.ToRuntime(new GodotAudioBusMuteBinding(busName, fallbackMuted), fallbackMuted);
    }

    public static void ToProjectSettingDefault<TOptions, TValue>(
        this OptionPropertyMappingBuilder<TOptions, TValue> builder,
        string settingPath,
        TValue fallbackDefault)
        where TOptions : class, new()
    {
        builder.ToRuntime(new GodotProjectSettingBinding<TValue>(settingPath, fallbackDefault), fallbackDefault);
    }
}
