using Fxyoge.DependencyInjection;
using Godot;

namespace Basic.Game;

public partial class Main : Control
{
    private IMyService? _myService;

    private IMyService? MyService => _myService ??= (Engine.GetMainLoop() as SceneTree)
        ?.Root
        .GetNodeOrNull<GameServices>("GameServices")
        ?.GetRequiredService<IMyService>();

    public override void _Ready()
    {
        GetNode<Label>("MessageLabel").Text = MyService?.GetMessage() ?? "GameServices autoload was not found.";
    }
}
