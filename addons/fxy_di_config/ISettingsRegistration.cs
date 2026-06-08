using System.Collections.Generic;
using System.Reflection;

namespace Fxyoge.DependencyInjection.Configuration;

public interface ISettingsRegistration<TOptions>
    where TOptions : class, new()
{
    string Section { get; }

    IReadOnlyList<IOptionPropertyMapping<TOptions>> Mappings { get; }
}

public interface IOptionPropertyMapping<TOptions>
    where TOptions : class, new()
{
    ConfigEntryDescriptor Descriptor { get; }

    PropertyInfo Property { get; }

    void LoadDefault(TOptions options);

    void LoadOverlay(TOptions options, IConfigOverlayStore store);

    void CaptureOverlay(TOptions options, IConfigOverlayStore store);

    object? CaptureRuntime();

    void Apply(TOptions options);

    void RestoreRuntime(object? snapshot);

    TValue GetValue<TValue>(TOptions options);

    void SetValue<TValue>(TOptions options, TValue value);

    void ResetOverlay(IConfigOverlayStore store);

    void CopyValue(TOptions source, TOptions target);
}
