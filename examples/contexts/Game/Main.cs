using System.Collections.Generic;
using Godot;

namespace ContextsExample.Game;

public partial class Main : Control
{
    private readonly List<VehicleActor> _actors = new();
    private RaceTrackView? _trackView;
    private ContextDiagnosticsInspector? _inspector;
    private CameraView _view = new(Vector2.Zero, 1f);

    public override void _Ready()
    {
        _trackView = GetNode<RaceTrackView>("TrackContext/TrackView");
        _inspector = GetNode<ContextDiagnosticsInspector>("Inspector");
        ConfigureInspectorPanel(_inspector);

        foreach (var node in GetTree().GetNodesInGroup(VehicleActor.SceneGroup))
        {
            if (node is VehicleActor actor)
            {
                _actors.Add(actor);
            }
        }

        if (_actors.Count > 0)
        {
            _view = _actors[0].GetCameraView(GetViewportRect().Size);
        }

        _trackView.SetActors(_actors);
        _trackView.SetView(_view);
        _inspector.Capture(_actors);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        foreach (var actor in _actors)
        {
            actor.Tick(delta);
        }

        if (_actors.Count > 0)
        {
            _view = _actors[0].GetCameraView(GetViewportRect().Size, delta);
        }

        _trackView?.SetView(_view);
    }

    private static void ConfigureInspectorPanel(Control inspector)
    {
        inspector.AnchorLeft = 0f;
        inspector.AnchorTop = 0f;
        inspector.AnchorRight = 0f;
        inspector.AnchorBottom = 1f;
        inspector.OffsetLeft = 12f;
        inspector.OffsetTop = 12f;
        inspector.OffsetRight = 690f;
        inspector.OffsetBottom = -12f;
    }
}
