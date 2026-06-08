using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotAudioBusMuteBinding : IRuntimeConfigBinding<bool>
{
    private readonly string _busName;

    public GodotAudioBusMuteBinding(string busName)
    {
        _busName = busName;
    }

    public bool CaptureDefault() => ReadCurrent();

    public bool ReadCurrent()
    {
        var busIndex = GetBusIndex();
        return AudioServer.IsBusMute(busIndex);
    }

    public void Apply(bool value)
    {
        var busIndex = GetBusIndex();
        AudioServer.SetBusMute(busIndex, value);
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
            throw new InvalidOperationException($"fxy_di_config could not find audio bus '{_busName}'.");
        }

        return busIndex;
    }
}
