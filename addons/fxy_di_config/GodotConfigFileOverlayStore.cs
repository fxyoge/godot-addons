using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotConfigFileOverlayStore : IConfigOverlayStore
{
    private readonly ConfigFile _configFile = new();
    private readonly string _path;

    public GodotConfigFileOverlayStore(string path = "user://settings.cfg")
    {
        _path = path;

        if (FileAccess.FileExists(_path))
        {
            var error = _configFile.Load(_path);
            if (error != Error.Ok)
            {
                GD.PushWarning($"fxy_di_config could not load '{_path}': {error}");
            }
        }
    }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
        if (typeof(TValue) == typeof(InputActionBinding))
        {
            if (!_configFile.HasSectionKey(section, $"{key}/key_code"))
            {
                value = default!;
                return false;
            }

            var keyCode = Convert.ToInt64(_configFile.GetValue(section, $"{key}/key_code").Obj);
            value = (TValue)(object)new InputActionBinding
            {
                KeyCode = keyCode,
                DisplayName = keyCode == 0 ? "Unbound" : OS.GetKeycodeString((Key)keyCode),
            };
            return true;
        }

        if (!_configFile.HasSectionKey(section, key))
        {
            value = default!;
            return false;
        }

        var stored = _configFile.GetValue(section, key).Obj;

        try
        {
            value = ConvertValue<TValue>(stored);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"fxy_di_config ignored invalid value '{section}/{key}' in '{_path}': {ex.Message}");
            value = default!;
            return false;
        }
    }

    public void Set<TValue>(string section, string key, TValue value)
    {
        if (value is InputActionBinding inputAction)
        {
            _configFile.SetValue(section, $"{key}/key_code", inputAction.KeyCode);
            return;
        }

        _configFile.SetValue(section, key, ToGodotValue(value));
    }

    public void Remove(string section, string key)
    {
        if (_configFile.HasSectionKey(section, $"{key}/key_code"))
        {
            _configFile.EraseSectionKey(section, $"{key}/key_code");
        }

        if (_configFile.HasSectionKey(section, key))
        {
            _configFile.EraseSectionKey(section, key);
        }
    }

    public void Save()
    {
        var error = _configFile.Save(_path);
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"fxy_di_config could not save '{_path}': {error}");
        }
    }

    private static TValue ConvertValue<TValue>(object? value)
    {
        if (value is null)
        {
            return default!;
        }

        if (value is TValue typed)
        {
            return typed;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

        if (targetType.IsEnum)
        {
            return (TValue)Enum.Parse(targetType, value.ToString()!, ignoreCase: true);
        }

        return (TValue)Convert.ChangeType(value, targetType);
    }

    private static Variant ToGodotValue<TValue>(TValue value)
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
                $"fxy_di_config cannot persist '{typeof(TValue).FullName}' as a Godot ConfigFile value. Map complex values through a Godot-native binding."),
        };
}
