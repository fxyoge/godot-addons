using System;
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
        builder.PersistAs(actionName).ToRuntime(new GodotInputActionBinding(actionName, (long)fallbackKey), new InputActionBinding
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
        builder.PersistAs($"{ToSnakeCase(busName)}/volume")
            .ToRuntime(new GodotAudioBusVolumeBinding(busName, fallbackLinearVolume), fallbackLinearVolume);
    }

    public static void ToAudioBusMute<TOptions>(
        this OptionPropertyMappingBuilder<TOptions, bool> builder,
        string busName,
        bool fallbackMuted)
        where TOptions : class, new()
    {
        builder.PersistAs($"{ToSnakeCase(busName)}/muted")
            .ToRuntime(new GodotAudioBusMuteBinding(busName, fallbackMuted), fallbackMuted);
    }

    public static void ToProjectSettingDefault<TOptions, TValue>(
        this OptionPropertyMappingBuilder<TOptions, TValue> builder,
        string settingPath,
        TValue fallbackDefault)
        where TOptions : class, new()
    {
        builder.PersistAs(settingPath)
            .ToRuntime(new GodotProjectSettingBinding<TValue>(settingPath, fallbackDefault), fallbackDefault);
    }

    private static string ToSnakeCase(string value)
    {
        Span<char> buffer = stackalloc char[value.Length * 2];
        var length = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsWhiteSpace(current) || current == '-')
            {
                buffer[length++] = '_';
                continue;
            }

            if (char.IsUpper(current))
            {
                if (i > 0 && length > 0 && buffer[length - 1] != '_' && !char.IsUpper(value[i - 1]))
                {
                    buffer[length++] = '_';
                }

                buffer[length++] = char.ToLowerInvariant(current);
                continue;
            }

            buffer[length++] = current;
        }

        return new string(buffer[..length]);
    }
}
