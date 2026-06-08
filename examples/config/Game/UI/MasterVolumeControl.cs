using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class MasterVolumeControl : HBoxContainer
{
    private ISettingsMonitor<AudioOptions>? _audio;
    private HSlider? _slider;
    private Label? _valueLabel;
    private System.IDisposable? _subscription;
    private bool _isDragging;

    public override void _Ready()
    {
        _audio = this.GetRequiredService<ISettingsMonitor<AudioOptions>>();
        _slider = GetNode<HSlider>("VolumeSlider");
        _valueLabel = GetNode<Label>("ValueLabel");

        _slider.ValueChanged += async value =>
        {
            RefreshLabel((float)value);
            if (!_isDragging)
            {
                await _audio.Set(options => options.MasterVolume, (float)value);
            }
        };
        _slider.DragStarted += () => _isDragging = true;
        _slider.DragEnded += async valueChanged =>
        {
            _isDragging = false;
            if (valueChanged)
            {
                await _audio.Set(options => options.MasterVolume, (float)_slider.Value);
            }
        };

        _subscription = _audio.OnChange(_ => CallDeferred(MethodName.Refresh));
        Refresh();
    }

    public override void _ExitTree()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Refresh()
    {
        if (_slider is null || _valueLabel is null)
        {
            return;
        }

        var volume = _audio!.CurrentValue.MasterVolume;
        _slider.SetValueNoSignal(volume);
        RefreshLabel(volume);
    }

    private void RefreshLabel(float volume)
    {
        if (_valueLabel is not null)
        {
            _valueLabel.Text = $"{volume:P0}";
        }
    }
}
