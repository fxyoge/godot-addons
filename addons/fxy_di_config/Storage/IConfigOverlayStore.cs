namespace Fxyoge.DependencyInjection.Configuration;

public interface IConfigOverlayStore
{
    bool TryGet<TValue>(string section, string key, out TValue value);

    void Set<TValue>(string section, string key, TValue value);

    void Remove(string section, string key);

    IConfigOverlayStoreSnapshot CreateSnapshot();

    void RestoreSnapshot(IConfigOverlayStoreSnapshot snapshot);

    void Save();
}

public interface IConfigOverlayStoreSnapshot
{
}
