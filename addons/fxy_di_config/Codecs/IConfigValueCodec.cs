namespace Fxyoge.DependencyInjection.Configuration;

public interface IConfigValueCodec<TValue>
{
    bool TryRead(IConfigOverlayStore store, string section, string key, out TValue value);

    void Write(IConfigOverlayStore store, string section, string key, TValue value);

    void Remove(IConfigOverlayStore store, string section, string key);
}
