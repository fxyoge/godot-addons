using System.Collections.Generic;

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

    void LoadDefault(TOptions options);

    void LoadOverlay(TOptions options, IConfigOverlayStore store);

    void CaptureOverlay(TOptions options, IConfigOverlayStore store);

    void Apply(TOptions options);

    void ResetOverlay(IConfigOverlayStore store);
}
