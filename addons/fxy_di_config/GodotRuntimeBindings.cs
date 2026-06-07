using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotInputActionBinding : IRuntimeConfigBinding<InputActionBinding>
{
    private readonly string _actionName;
    private readonly long _fallbackKeyCode;

    public GodotInputActionBinding(string actionName, long fallbackKeyCode)
    {
        _actionName = actionName;
        _fallbackKeyCode = fallbackKeyCode;
    }

    public InputActionBinding ReadDefault()
    {
        EnsureAction();

        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                return CreateBinding((long)keyEvent.Keycode);
            }
        }

        return CreateBinding(_fallbackKeyCode);
    }

    public void Apply(InputActionBinding value)
    {
        EnsureAction();
        InputMap.ActionEraseEvents(_actionName);

        if (value.KeyCode == 0)
        {
            return;
        }

        InputMap.ActionAddEvent(_actionName, new InputEventKey
        {
            Keycode = (Key)value.KeyCode,
        });
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(InputActionBinding),
            ConfigValueSource.InputMap,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.KeyBinding));

    private void EnsureAction()
    {
        if (!InputMap.HasAction(_actionName))
        {
            InputMap.AddAction(_actionName);
        }
    }

    private static InputActionBinding CreateBinding(long keyCode)
        => new()
        {
            KeyCode = keyCode,
            DisplayName = keyCode == 0 ? "Unbound" : OS.GetKeycodeString((Key)keyCode),
        };
}

public sealed class GodotAudioBusVolumeBinding : IRuntimeConfigBinding<float>
{
    private readonly string _busName;
    private readonly float _fallbackLinearVolume;

    public GodotAudioBusVolumeBinding(string busName, float fallbackLinearVolume)
    {
        _busName = busName;
        _fallbackLinearVolume = fallbackLinearVolume;
    }

    public float ReadDefault()
    {
        var busIndex = GetBusIndex();
        if (busIndex < 0)
        {
            return _fallbackLinearVolume;
        }

        return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex));
    }

    public void Apply(float value)
    {
        var busIndex = GetBusIndex();
        if (busIndex < 0)
        {
            return;
        }

        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(Mathf.Clamp(value, 0.0001f, 1.0f)));
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(float),
            ConfigValueSource.AudioBus,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.Slider, Min: 0, Max: 1, Step: 0.01));

    private int GetBusIndex()
    {
        var busIndex = AudioServer.GetBusIndex(_busName);
        if (busIndex < 0)
        {
            GD.PushWarning($"fxy_di_config could not find audio bus '{_busName}'.");
        }

        return busIndex;
    }
}

public sealed class GodotAudioBusMuteBinding : IRuntimeConfigBinding<bool>
{
    private readonly string _busName;
    private readonly bool _fallbackMuted;

    public GodotAudioBusMuteBinding(string busName, bool fallbackMuted)
    {
        _busName = busName;
        _fallbackMuted = fallbackMuted;
    }

    public bool ReadDefault()
    {
        var busIndex = GetBusIndex();
        return busIndex < 0 ? _fallbackMuted : AudioServer.IsBusMute(busIndex);
    }

    public void Apply(bool value)
    {
        var busIndex = GetBusIndex();
        if (busIndex >= 0)
        {
            AudioServer.SetBusMute(busIndex, value);
        }
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(bool),
            ConfigValueSource.AudioBus,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.Toggle));

    private int GetBusIndex()
    {
        var busIndex = AudioServer.GetBusIndex(_busName);
        if (busIndex < 0)
        {
            GD.PushWarning($"fxy_di_config could not find audio bus '{_busName}'.");
        }

        return busIndex;
    }
}

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
        // V1 treats ProjectSettings as a default source. User overrides are exposed
        // through typed options but are not written back to project.godot.
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
