using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class GameTitleControl : HBoxContainer
{
    private ISettingsMonitor<ProjectDefaultsOptions>? _project;
    private LineEdit? _titleInput;
    private System.IDisposable? _subscription;
    private bool _isRefreshing;

    public override void _Ready()
    {
        _project = this.GetRequiredService<ISettingsMonitor<ProjectDefaultsOptions>>();
        _titleInput = GetNode<LineEdit>("TitleInput");

        _titleInput.TextChanged += async value =>
        {
            if (!_isRefreshing)
            {
                await _project.Set(options => options.GameTitle, value);
            }
        };

        _subscription = _project.OnChange(_ => CallDeferred(MethodName.Refresh));
        Refresh();
    }

    public override void _ExitTree()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Refresh()
    {
        if (_titleInput is null || _titleInput.HasFocus())
        {
            return;
        }

        _isRefreshing = true;
        _titleInput.Text = _project!.CurrentValue.GameTitle;
        _isRefreshing = false;
    }
}
