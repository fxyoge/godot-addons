using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotProjectSettingBinding<TValue> : IRuntimeConfigBinding<TValue>
{
    private readonly string _settingPath;
    private readonly TValue _fallbackDefault;

    public GodotProjectSettingBinding(string settingPath, TValue fallbackDefault)
    {
        _settingPath = settingPath;
        _fallbackDefault = fallbackDefault;
    }

    public TValue ReadDefault()
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

            return value is null ? _fallbackDefault : (TValue)Convert.ChangeType(value, typeof(TValue));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"fxy_di_config could not read ProjectSettings '{_settingPath}': {ex.Message}");
            return _fallbackDefault;
        }
    }

    public void Apply(TValue value)
    {
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(TValue),
            ConfigValueSource.ProjectSettings,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint);
}
