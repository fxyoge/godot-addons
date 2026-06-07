namespace Fxyoge.DependencyInjection.Configuration;

using System;

public sealed class InputActionBindingConfigValueCodec : IConfigValueCodec<InputActionBinding>
{
    public static InputActionBindingConfigValueCodec Instance { get; } = new();

    private readonly Func<long, string>? _displayNameFormatter;

    public InputActionBindingConfigValueCodec(Func<long, string>? displayNameFormatter = null)
    {
        _displayNameFormatter = displayNameFormatter;
    }

    public bool TryRead(
        IConfigOverlayStore store,
        string section,
        string key,
        out InputActionBinding value)
    {
        if (!store.TryGet<long>(section, GetKeyCodeKey(key), out var keyCode))
        {
            value = default!;
            return false;
        }

        value = InputActionBinding.FromKeyCode(keyCode, _displayNameFormatter);
        return true;
    }

    public void Write(IConfigOverlayStore store, string section, string key, InputActionBinding value)
        => store.Set(section, GetKeyCodeKey(key), value.KeyCode);

    public void Remove(IConfigOverlayStore store, string section, string key)
        => store.Remove(section, GetKeyCodeKey(key));

    private static string GetKeyCodeKey(string key) => $"{key}/key_code";
}
