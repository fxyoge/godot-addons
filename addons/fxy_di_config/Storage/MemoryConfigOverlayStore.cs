using System.Collections.Generic;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class MemoryConfigOverlayStore : IConfigOverlayStore
{
    private readonly Dictionary<string, object?> _values = new();

    public int SaveCount { get; private set; }

    public System.Exception? SaveException { get; set; }

    public bool TryGet<TValue>(string section, string key, out TValue value)
    {
        if (!_values.TryGetValue(GetStoreKey(section, key), out var stored))
        {
            value = default!;
            return false;
        }

        if (stored is TValue typed)
        {
            value = typed;
            return true;
        }

        if (stored is null && default(TValue) is null)
        {
            value = default!;
            return true;
        }

        throw new System.InvalidOperationException(
            $"Stored config value '{section}/{key}' is '{stored?.GetType().FullName ?? "null"}', not '{typeof(TValue).FullName}'.");
    }

    public void Set<TValue>(string section, string key, TValue value)
    {
        _values[GetStoreKey(section, key)] = value;
    }

    public void Remove(string section, string key)
    {
        _values.Remove(GetStoreKey(section, key));
    }

    public IConfigOverlayStoreSnapshot CreateSnapshot()
        => new Snapshot(new Dictionary<string, object?>(_values));

    public void RestoreSnapshot(IConfigOverlayStoreSnapshot snapshot)
    {
        if (snapshot is not Snapshot memorySnapshot)
        {
            throw new System.ArgumentException(
                $"Snapshot must be created by '{nameof(MemoryConfigOverlayStore)}'.",
                nameof(snapshot));
        }

        _values.Clear();
        foreach (var (key, value) in memorySnapshot.Values)
        {
            _values[key] = value;
        }
    }

    public void Save()
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        SaveCount++;
    }

    private static string GetStoreKey(string section, string key) => $"{section}/{key}";

    private sealed record Snapshot(Dictionary<string, object?> Values) : IConfigOverlayStoreSnapshot;
}
