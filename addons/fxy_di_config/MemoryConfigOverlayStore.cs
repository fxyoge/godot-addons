using System.Collections.Generic;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class MemoryConfigOverlayStore : IConfigOverlayStore
{
    private readonly Dictionary<string, object?> _values = new();

    public int SaveCount { get; private set; }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
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
        _values[GetStoreKey(section, key)] = value;
    }

    public void Remove(string section, string key)
    {
        _values.Remove(GetStoreKey(section, key));
    }

    public void Save()
    {
        SaveCount++;
    }

    private static string GetStoreKey(string section, string key) => $"{section}/{key}";
}
