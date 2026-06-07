using System.Collections.Generic;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class SettingsRegistration<TOptions> : ISettingsRegistration<TOptions>
    where TOptions : class, new()
{
    public SettingsRegistration(string section, IReadOnlyList<IOptionPropertyMapping<TOptions>> mappings)
    {
        Section = section;
        Mappings = mappings;
    }

    public string Section { get; }

    public IReadOnlyList<IOptionPropertyMapping<TOptions>> Mappings { get; }
}
