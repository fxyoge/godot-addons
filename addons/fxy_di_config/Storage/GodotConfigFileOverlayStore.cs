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
            ThrowIfLoadFailed(_path, _configFile.Load(_path));
        }
    }

    internal static void ThrowIfLoadFailed(string path, Error error)
    {
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"fxy_di_config could not load '{path}': {error}");
        }
    }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
        if (!_configFile.HasSectionKey(section, key))
        {
            value = default!;
            return false;
        }

        var stored = _configFile.GetValue(section, key).Obj;

        value = ConvertValue<TValue>(stored);
        return true;
    }

    public void Set<TValue>(string section, string key, TValue value)
    {
        _configFile.SetValue(section, key, ToGodotValue(value));
    }

    public void Remove(string section, string key)
    {
        if (_configFile.HasSectionKey(section, key))
        {
            _configFile.EraseSectionKey(section, key);
        }
    }

    public IConfigOverlayStoreSnapshot CreateSnapshot()
        => new Snapshot(CloneConfigFile(_configFile));

    public void RestoreSnapshot(IConfigOverlayStoreSnapshot snapshot)
    {
        if (snapshot is not Snapshot godotSnapshot)
        {
            throw new ArgumentException(
                $"Snapshot must be created by '{nameof(GodotConfigFileOverlayStore)}'.",
                nameof(snapshot));
        }

        CopyConfigFile(godotSnapshot.ConfigFile, _configFile);
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
            if (default(TValue) is null)
            {
                return default!;
            }

            throw new InvalidOperationException(
                $"Cannot convert null config value to '{typeof(TValue).FullName}'.");
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

    private static ConfigFile CloneConfigFile(ConfigFile source)
    {
        var clone = new ConfigFile();
        CopyConfigFile(source, clone);
        return clone;
    }

    private static void CopyConfigFile(ConfigFile source, ConfigFile target)
    {
        foreach (var section in target.GetSections())
        {
            target.EraseSection(section);
        }

        foreach (var section in source.GetSections())
        {
            foreach (var key in source.GetSectionKeys(section))
            {
                target.SetValue(section, key, source.GetValue(section, key));
            }
        }
    }

    private sealed record Snapshot(ConfigFile ConfigFile) : IConfigOverlayStoreSnapshot;
}
