using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotAudioBusVolumeBinding : IRuntimeConfigBinding<float>
{
    private readonly string _busName;
    private readonly float _fallbackLinearVolume;

    public GodotAudioBusVolumeBinding(string busName, float fallbackLinearVolume)
    {
        _busName = busName;
        _fallbackLinearVolume = fallbackLinearVolume;
    }

    public float ReadDefault()
    {
        var busIndex = GetBusIndex();
        if (busIndex < 0)
        {
            return _fallbackLinearVolume;
        }

        return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex));
    }

    public void Apply(float value)
    {
        var busIndex = GetBusIndex();
        if (busIndex < 0)
        {
            return;
        }

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
            GD.PushWarning($"fxy_di_config could not find audio bus '{_busName}'.");
        }

        return busIndex;
    }
}
