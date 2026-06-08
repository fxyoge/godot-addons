namespace Fxyoge.DependencyInjection.Configuration;

public interface IRuntimeConfigBinding<TValue>
{
    TValue CaptureDefault();

    TValue ReadCurrent();

    void Apply(TValue value);

    ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint);
}
