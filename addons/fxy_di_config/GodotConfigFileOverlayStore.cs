using System;
using System.Text.Json;
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
                GD.PushWarning($"Fxy DI Config could not load '{_path}': {error}");
            }
        }
    }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
        if (!_configFile.HasSectionKey(section, key))
        {
            value = default!;
            return false;
        }

        var stored = _configFile.GetValue(section, key).AsString();

        try
        {
            value = JsonSerializer.Deserialize<TValue>(stored)!;
            return value is not null;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Fxy DI Config ignored invalid value '{section}/{key}' in '{_path}': {ex.Message}");
            value = default!;
            return false;
        }
    }

    public void Set<TValue>(string section, string key, TValue value)
    {
        _configFile.SetValue(section, key, JsonSerializer.Serialize(value));
    }

    public void Remove(string section, string key)
    {
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
            throw new InvalidOperationException($"Fxy DI Config could not save '{_path}': {error}");
        }
    }
}
