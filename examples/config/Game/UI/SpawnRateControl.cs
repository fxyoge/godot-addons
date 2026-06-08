using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class SpawnRateControl : HBoxContainer
{
    private ISettingsMonitor<GameplayOptions>? _gameplay;
    private HSlider? _slider;
    private Label? _valueLabel;
    private System.IDisposable? _subscription;
    private bool _isDragging;

    public override void _Ready()
    {
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
        _slider = GetNode<HSlider>("SpawnSlider");
        _valueLabel = GetNode<Label>("ValueLabel");

        _slider.ValueChanged += async value =>
        {
            RefreshLabel((float)value);
            if (!_isDragging)
            {
                await _gameplay.Set(options => options.SpawnRate, (float)value);
            }
        };
        _slider.DragStarted += () => _isDragging = true;
        _slider.DragEnded += async valueChanged =>
        {
            _isDragging = false;
            if (valueChanged)
            {
                await _gameplay.Set(options => options.SpawnRate, (float)_slider.Value);
            }
        };

        _subscription = _gameplay.OnChange(_ => CallDeferred(MethodName.Refresh));
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

        var spawnRate = _gameplay!.CurrentValue.SpawnRate;
        _slider.SetValueNoSignal(spawnRate);
        RefreshLabel(spawnRate);
    }

    private void RefreshLabel(float spawnRate)
    {
        if (_valueLabel is not null)
        {
            _valueLabel.Text = $"{spawnRate:0.00}x";
        }
    }
}
