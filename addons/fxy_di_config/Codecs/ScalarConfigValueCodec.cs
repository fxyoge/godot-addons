namespace Fxyoge.DependencyInjection.Configuration;

public sealed class ScalarConfigValueCodec<TValue> : IConfigValueCodec<TValue>
{
    public static ScalarConfigValueCodec<TValue> Instance { get; } = new();

    private ScalarConfigValueCodec()
    {
    }

    public bool TryRead(IConfigOverlayStore store, string section, string key, out TValue value)
        => store.TryGet(section, key, out value);

    public void Write(IConfigOverlayStore store, string section, string key, TValue value)
        => store.Set(section, key, value);

    public void Remove(IConfigOverlayStore store, string section, string key)
        => store.Remove(section, key);
}
