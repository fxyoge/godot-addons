using System;

namespace Fxyoge.DependencyInjection.Configuration;

internal sealed class OptionPropertyMapping<TOptions, TValue> : IOptionPropertyMapping<TOptions>
    where TOptions : class, new()
{
    private readonly Func<TOptions, TValue> _getValue;
    private readonly Action<TOptions, TValue> _setValue;
    private readonly TValue _fallbackDefault;
    private readonly IRuntimeConfigBinding<TValue>? _runtimeBinding;
    private readonly IConfigValueCodec<TValue> _codec;

    public OptionPropertyMapping(
        string section,
        string key,
        Func<TOptions, TValue> getValue,
        Action<TOptions, TValue> setValue,
        TValue fallbackDefault,
        ConfigEntryDescriptor descriptor,
        IRuntimeConfigBinding<TValue>? runtimeBinding,
        IConfigValueCodec<TValue> codec)
    {
        Section = section;
        Key = key;
        _getValue = getValue;
        _setValue = setValue;
        _fallbackDefault = fallbackDefault;
        Descriptor = descriptor;
        _runtimeBinding = runtimeBinding;
        _codec = codec;
    }

    private string Section { get; }

    private string Key { get; }

    public ConfigEntryDescriptor Descriptor { get; }

    public void LoadDefault(TOptions options)
    {
        _setValue(options, _runtimeBinding is null ? _fallbackDefault : _runtimeBinding.ReadDefault());
    }

    public void LoadOverlay(TOptions options, IConfigOverlayStore store)
    {
        if (_codec.TryRead(store, Section, Key, out var value))
        {
            _setValue(options, value);
        }
    }

    public void CaptureOverlay(TOptions options, IConfigOverlayStore store)
    {
        _codec.Write(store, Section, Key, _getValue(options));
    }

    public void Apply(TOptions options)
    {
        _runtimeBinding?.Apply(_getValue(options));
    }

    public void ResetOverlay(IConfigOverlayStore store)
    {
        _codec.Remove(store, Section, Key);
    }

    public void CopyValue(TOptions source, TOptions target)
    {
        _setValue(target, ConfigMappedValue.Copy(_getValue(source)));
    }
}
