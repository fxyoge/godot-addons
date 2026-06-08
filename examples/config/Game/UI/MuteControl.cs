using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class MuteControl : HBoxContainer
{
    private ISettingsMonitor<AudioOptions>? _audio;
    private CheckBox? _checkBox;
    private System.IDisposable? _subscription;

    public override void _Ready()
    {
        _audio = this.GetRequiredService<ISettingsMonitor<AudioOptions>>();
        _checkBox = GetNode<CheckBox>("MuteCheckBox");

        _checkBox.Toggled += async value => await _audio.Set(options => options.Muted, value);

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
        _checkBox?.SetPressedNoSignal(_audio!.CurrentValue.Muted);
    }
}
