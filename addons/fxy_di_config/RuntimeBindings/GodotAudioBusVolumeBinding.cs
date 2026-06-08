using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotAudioBusVolumeBinding : IRuntimeConfigBinding<float>
{
    private readonly string _busName;

    public GodotAudioBusVolumeBinding(string busName)
    {
        _busName = busName;
    }

    public float CaptureDefault() => ReadCurrent();

    public float ReadCurrent()
    {
        var busIndex = GetBusIndex();
        return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex));
    }

    public void Apply(float value)
    {
        var busIndex = GetBusIndex();
        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(Mathf.Clamp(value, 0.0001f, 1.0f)));
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(float),
            ConfigValueSource.AudioBus,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.Slider, Min: 0, Max: 1, Step: 0.01));

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
