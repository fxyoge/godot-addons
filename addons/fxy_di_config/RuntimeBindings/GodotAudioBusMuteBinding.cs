using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotAudioBusMuteBinding : IRuntimeConfigBinding<bool>
{
    private readonly string _busName;
    private readonly bool _fallbackMuted;

    public GodotAudioBusMuteBinding(string busName, bool fallbackMuted)
    {
        _busName = busName;
        _fallbackMuted = fallbackMuted;
    }

    public bool ReadDefault()
    {
        var busIndex = GetBusIndex();
        return busIndex < 0 ? _fallbackMuted : AudioServer.IsBusMute(busIndex);
    }

    public void Apply(bool value)
    {
        var busIndex = GetBusIndex();
        if (busIndex >= 0)
        {
            AudioServer.SetBusMute(busIndex, value);
        }
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(bool),
            ConfigValueSource.AudioBus,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.Toggle));

    private int GetBusIndex()
    {
        var busIndex = AudioServer.GetBusIndex(_busName);
        if (busIndex < 0)
        {
            GD.PushWarning($"fxy_di_config could not find audio bus '{_busName}'.");
        }

        return busIndex;
    }
}
