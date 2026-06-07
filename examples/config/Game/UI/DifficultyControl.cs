using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class DifficultyControl : HBoxContainer
{
    private ISettingsMonitor<GameplayOptions>? _gameplay;
    private OptionButton? _select;
    private System.IDisposable? _subscription;

    public override void _Ready()
    {
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
        _select = GetNode<OptionButton>("DifficultySelect");

        _select.ItemSelected += async index =>
        {
            await _gameplay.Update(options => options.Difficulty = _select.GetItemText((int)index));
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
        if (_select is null)
        {
            return;
        }

        _select.Select(GetDifficultyIndex(_gameplay!.CurrentValue.Difficulty));
    }

    private static int GetDifficultyIndex(string difficulty)
        => difficulty switch
        {
            "Easy" => 0,
            "Normal" => 1,
            "Hard" => 2,
            _ => 1,
        };
}
