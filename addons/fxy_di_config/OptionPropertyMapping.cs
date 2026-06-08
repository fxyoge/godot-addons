using System;
using System.Reflection;

namespace Fxyoge.DependencyInjection.Configuration;

internal sealed class OptionPropertyMapping<TOptions, TValue> : IOptionPropertyMapping<TOptions>
    where TOptions : class, new()
{
    private readonly Func<TOptions, TValue> _getValue;
    private readonly Action<TOptions, TValue> _setValue;
    private readonly TValue _defaultValue;
    private readonly IRuntimeConfigBinding<TValue>? _runtimeBinding;
    private readonly IConfigValueCodec<TValue> _codec;

    public OptionPropertyMapping(
        string section,
        string key,
        PropertyInfo property,
        Func<TOptions, TValue> getValue,
        Action<TOptions, TValue> setValue,
        TValue defaultValue,
        ConfigEntryDescriptor descriptor,
        IRuntimeConfigBinding<TValue>? runtimeBinding,
        IConfigValueCodec<TValue> codec)
    {
        Section = section;
        Key = key;
        Property = property;
        _getValue = getValue;
        _setValue = setValue;
        _defaultValue = defaultValue;
        Descriptor = descriptor;
        _runtimeBinding = runtimeBinding;
        _codec = codec;
    }

    private string Section { get; }

    private string Key { get; }

    public ConfigEntryDescriptor Descriptor { get; }

    public PropertyInfo Property { get; }

    public void LoadDefault(TOptions options)
    {
        _setValue(options, _runtimeBinding is null ? _defaultValue : _runtimeBinding.ReadDefault());
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

    public object? CaptureRuntime()
        => _runtimeBinding is null
            ? null
            : ConfigMappedValue.Copy(_runtimeBinding.ReadCurrent());

    public void Apply(TOptions options)
    {
        _runtimeBinding?.Apply(_getValue(options));
    }

    public void RestoreRuntime(object? snapshot)
    {
        if (_runtimeBinding is null)
        {
            return;
        }

        if (snapshot is TValue typed)
        {
            _runtimeBinding.Apply(typed);
            return;
        }

        if (snapshot is null && default(TValue) is null)
        {
            _runtimeBinding.Apply(default!);
            return;
        }

        throw new InvalidOperationException(
            $"Runtime snapshot for mapped setting '{Section}/{Key}' is not '{typeof(TValue).FullName}'.");
    }

    public TRequested GetValue<TRequested>(TOptions options)
    {
        var value = _getValue(options);
        if (value is TRequested typed)
        {
            return typed;
        }

        if (value is null)
        {
            return default!;
        }

        throw new InvalidOperationException(
            $"Mapped setting '{Section}/{Key}' is '{typeof(TValue).FullName}', not '{typeof(TRequested).FullName}'.");
    }

    public void SetValue<TRequested>(TOptions options, TRequested value)
    {
        if (value is TValue typed)
        {
            _setValue(options, typed);
            return;
        }

        if (value is null && default(TValue) is null)
        {
            _setValue(options, default!);
            return;
        }

        throw new InvalidOperationException(
            $"Mapped setting '{Section}/{Key}' is '{typeof(TValue).FullName}', not '{typeof(TRequested).FullName}'.");
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
