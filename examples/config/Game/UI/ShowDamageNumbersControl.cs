using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class ShowDamageNumbersControl : HBoxContainer
{
    private ISettingsMonitor<GameplayOptions>? _gameplay;
    private CheckBox? _checkBox;
    private System.IDisposable? _subscription;

    public override void _Ready()
    {
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
        _checkBox = GetNode<CheckBox>("DamageNumbersCheckBox");

        _checkBox.Toggled += async value => await _gameplay.Set(options => options.ShowDamageNumbers, value);

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
        _checkBox?.SetPressedNoSignal(_gameplay!.CurrentValue.ShowDamageNumbers);
    }
}
