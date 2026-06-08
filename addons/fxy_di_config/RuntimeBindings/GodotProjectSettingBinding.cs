using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotProjectSettingBinding<TValue> : IRuntimeConfigBinding<TValue>
{
    private readonly string _settingPath;
    private readonly TValue _fallbackDefault;
    private readonly bool _runtimeMutable;
    private readonly bool _requiresRestart;

    public GodotProjectSettingBinding(
        string settingPath,
        TValue fallbackDefault,
        bool runtimeMutable = true,
        bool requiresRestart = false)
    {
        _settingPath = settingPath;
        _fallbackDefault = fallbackDefault;
        _runtimeMutable = runtimeMutable;
        _requiresRestart = requiresRestart;
    }

    public TValue ReadDefault()
        => ReadCurrent();

    public TValue ReadCurrent()
    {
        if (!ProjectSettings.HasSetting(_settingPath))
        {
            return _fallbackDefault;
        }

        try
        {
            var value = ProjectSettings.GetSetting(_settingPath).Obj;
            if (value is TValue typed)
            {
                return typed;
            }

            return value is null ? _fallbackDefault : ConvertValue(value);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"fxy_di_config could not read ProjectSettings '{_settingPath}': {ex.Message}");
            return _fallbackDefault;
        }
    }

    public void Apply(TValue value)
    {
        if (!_runtimeMutable)
        {
            return;
        }

        try
        {
            ProjectSettings.SetSetting(_settingPath, ToGodotValue(value));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"fxy_di_config could not apply ProjectSettings '{_settingPath}': {ex.Message}");
        }
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(TValue),
            ConfigValueSource.ProjectSettings,
            Writable: true,
            RuntimeMutable: _runtimeMutable,
            RequiresRestart: _requiresRestart,
            uiHint);

    private static TValue ConvertValue(object value)
    {
        var targetType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

        if (targetType.IsEnum)
        {
            return (TValue)Enum.Parse(targetType, value.ToString()!, ignoreCase: true);
        }

        return (TValue)Convert.ChangeType(value, targetType);
    }

    private static Variant ToGodotValue(TValue value)
        => value switch
        {
            null => default,
            string typed => typed,
            bool typed => typed,
            byte typed => typed,
            short typed => typed,
            int typed => typed,
            long typed => typed,
            float typed => typed,
            double typed => typed,
            decimal typed => (double)typed,
            Enum typed => typed.ToString(),
            _ => throw new NotSupportedException(
                $"fxy_di_config cannot apply '{typeof(TValue).FullName}' as a Godot ProjectSettings value."),
        };
}
