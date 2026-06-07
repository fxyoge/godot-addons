using System.Collections.Generic;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class MemoryConfigOverlayStore : IConfigOverlayStore
{
    private readonly Dictionary<string, object?> _values = new();

    public int SaveCount { get; private set; }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
        if (typeof(TValue) == typeof(InputActionBinding)
            && _values.TryGetValue(GetStoreKey(section, $"{key}/key_code"), out var keyCode))
        {
            value = (TValue)(object)new InputActionBinding
            {
                KeyCode = System.Convert.ToInt64(keyCode),
                DisplayName = keyCode?.ToString() ?? string.Empty,
            };
            return true;
        }

        if (_values.TryGetValue(GetStoreKey(section, key), out var stored) && stored is TValue typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set<TValue>(string section, string key, TValue value)
    {
        if (value is InputActionBinding inputAction)
        {
            _values[GetStoreKey(section, $"{key}/key_code")] = inputAction.KeyCode;
            return;
        }

        _values[GetStoreKey(section, key)] = value;
    }

    public void Remove(string section, string key)
    {
        _values.Remove(GetStoreKey(section, $"{key}/key_code"));
        _values.Remove(GetStoreKey(section, key));
    }

    public void Save()
    {
        SaveCount++;
    }

    private static string GetStoreKey(string section, string key) => $"{section}/{key}";
}
