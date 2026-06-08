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

    public override void _Ready()
    {
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
        _slider = GetNode<HSlider>("SpawnSlider");
        _valueLabel = GetNode<Label>("ValueLabel");

        _slider.ValueChanged += async value =>
        {
            await _gameplay.Set(options => options.SpawnRate, (float)value);
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
        _valueLabel.Text = $"{spawnRate:0.00}x";
    }
}
