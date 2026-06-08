using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotProjectSettingBinding<TValue> : IRuntimeConfigBinding<TValue>
{
    private readonly string _settingPath;
    private readonly bool _runtimeMutable;
    private readonly bool _requiresRestart;

    public GodotProjectSettingBinding(
        string settingPath,
        bool runtimeMutable = true,
        bool requiresRestart = false)
    {
        _settingPath = settingPath;
        _runtimeMutable = runtimeMutable;
        _requiresRestart = requiresRestart;
    }

    public TValue ReadDefault()
        => ReadProjectSetting();

    public TValue ReadCurrent() => ReadProjectSetting();

    private TValue ReadProjectSetting()
    {
        if (!ProjectSettings.HasSetting(_settingPath))
        {
            throw new InvalidOperationException(
                $"ProjectSettings '{_settingPath}' does not exist and cannot be used as a config default.");
        }

        var value = ProjectSettings.GetSetting(_settingPath).Obj;
        if (value is TValue typed)
        {
            return typed;
        }

        if (value is null)
        {
            if (default(TValue) is null)
            {
                return default!;
            }

            throw new InvalidOperationException(
                $"ProjectSettings '{_settingPath}' is null and cannot be read as '{typeof(TValue).FullName}'.");
        }

        return ConvertValue(value);
    }

    public void Apply(TValue value)
    {
        if (!_runtimeMutable)
        {
            return;
        }

        ProjectSettings.SetSetting(_settingPath, ToGodotValue(value));
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
