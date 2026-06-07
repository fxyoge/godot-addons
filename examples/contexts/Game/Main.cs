using System.Collections.Generic;
using System.Linq;
using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class Main : Control
{
    private readonly List<RaceActor> _actors = new();
    private Label? _hud;
    private ITrackSession? _track;
    private CameraView _view = new(Vector2.Zero, 1f);

    public override void _Ready()
    {
        _hud = new Label
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            OffsetLeft = 16,
            OffsetTop = 12,
            OffsetRight = -16,
            OffsetBottom = 150,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _hud.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_hud);

        var trackContext = new Node { Name = "TrackContext" };
        AddChild(trackContext);
        trackContext.AddToGroup("track:grand-prix");
        _track = trackContext.GetRequiredService<ITrackSession>();

        var course = _track.Course;
        AddActor(
            "Player",
            new VehicleState { Position = course.GetStartPosition(1), Heading = -0.16f },
            "mode:runtime",
            "track:grand-prix",
            "entrant:player",
            "player:1",
            "camera:chase");

        AddActor(
            "Rival",
            new VehicleState { Position = course.GetStartPosition(2), Heading = -0.16f },
            "mode:runtime",
            "track:grand-prix",
            "entrant:rival",
            "rival:blue",
            "camera:chase");

        AddActor(
            "Ghost",
            new VehicleState { Position = course.GetStartPosition(3), Heading = -0.16f },
            "mode:replay",
            "track:grand-prix",
            "recording:best-lap",
            "camera:broadcast");

        AddActor(
            "Garage",
            new VehicleState { Position = course.GaragePosition, Heading = 0.6f },
            "mode:preview",
            "track:grand-prix",
            "vehicle:kart",
            "camera:garage");

        _view = _actors[0].Controller.GetCameraView(_actors[0].State, GetViewportRect().Size);
        SetProcess(true);
        UpdateHud();
    }

    public override void _Process(double delta)
    {
        foreach (var actor in _actors)
        {
            actor.Controller.Tick(actor.State, delta);
        }

        var focus = _actors[0];
        _view = focus.Controller.GetCameraView(focus.State, GetViewportRect().Size);
        UpdateHud();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_track is null)
        {
            return;
        }

        DrawWorldBackground(_track.Course);
        DrawCourse(_track.Course);
        DrawGarage(_track.Course);

        foreach (var actor in _actors)
        {
            DrawActor(actor);
        }

        DrawMiniMap(_track.Course);
    }

    private void AddActor(string name, VehicleState state, params string[] groups)
    {
        var node = new Node { Name = name };
        AddChild(node);

        foreach (var group in groups)
        {
            node.AddToGroup(group);
        }

        _actors.Add(new RaceActor(
            name,
            node,
            state,
            node.GetRequiredService<VehicleController>(),
            node.GetRequiredService<VehicleHudModel>(),
            groups));
    }

    private void UpdateHud()
    {
        if (_hud is null || _track is null || _actors.Count == 0)
        {
            return;
        }

        var player = _actors[0];
        var lines = new List<string>
        {
            "Contextual Grand Prix",
            "Drive: W/A/S/D. Camera is the player's contextual chase rig.",
            $"Shared course session: {_track.Id} | surface: {player.Controller.SurfaceLabel}",
            player.Hud.BuildLine(player.Name),
            _actors[1].Hud.BuildLine(_actors[1].Name),
            _actors[2].Hud.BuildLine(_actors[2].Name),
            $"Player modifiers: {player.Controller.ModifierLabel}",
            $"Ghost groups: {string.Join(", ", _actors[2].Groups)}",
        };

        _hud.Text = string.Join('\n', lines);
    }

    private void DrawWorldBackground(RaceCourse course)
    {
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0.07f, 0.18f, 0.11f));

        var topLeft = WorldToScreen(course.Bounds.Position);
        var bottomRight = WorldToScreen(course.Bounds.End);
        DrawRect(
            new Rect2(topLeft, bottomRight - topLeft),
            new Color(0.08f, 0.24f, 0.14f),
            filled: false,
            width: 3f);

        for (var x = 400; x < course.Bounds.End.X; x += 400)
        {
            DrawLine(
                WorldToScreen(new Vector2(x, course.Bounds.Position.Y)),
                WorldToScreen(new Vector2(x, course.Bounds.End.Y)),
                new Color(0.12f, 0.26f, 0.17f, 0.25f),
                1f);
        }

        for (var y = 400; y < course.Bounds.End.Y; y += 400)
        {
            DrawLine(
                WorldToScreen(new Vector2(course.Bounds.Position.X, y)),
                WorldToScreen(new Vector2(course.Bounds.End.X, y)),
                new Color(0.12f, 0.26f, 0.17f, 0.25f),
                1f);
        }
    }

    private void DrawCourse(RaceCourse course)
    {
        DrawCourseLine(course, course.RoadWidth + 58f, new Color(0.05f, 0.09f, 0.07f));
        DrawCourseLine(course, course.RoadWidth + 26f, new Color(0.70f, 0.73f, 0.68f));
        DrawCourseLine(course, course.RoadWidth, new Color(0.16f, 0.17f, 0.18f));
        DrawCourseLine(course, 5f, new Color(0.96f, 0.88f, 0.42f, 0.72f));

        for (var index = 0; index < course.Checkpoints.Count; index++)
        {
            DrawCheckpointGate(course, index);
        }
    }

    private void DrawCourseLine(RaceCourse course, float width, Color color)
    {
        for (var index = 0; index < course.CenterLine.Count; index++)
        {
            var start = course.CenterLine[index];
            var end = course.CenterLine[(index + 1) % course.CenterLine.Count];
            DrawLine(WorldToScreen(start), WorldToScreen(end), color, width * _view.Zoom);
            DrawCircle(WorldToScreen(start), width * _view.Zoom * 0.5f, color);
        }
    }

    private void DrawCheckpointGate(RaceCourse course, int index)
    {
        var checkpoint = course.Checkpoints[index];
        var previous = course.CenterLine[(index + course.CenterLine.Count - 1) % course.CenterLine.Count];
        var next = course.CenterLine[(index + 1) % course.CenterLine.Count];
        var side = (next - previous).Normalized().Rotated(Mathf.Pi * 0.5f);
        var half = course.RoadWidth * 0.47f;
        var color = index == 0
            ? new Color(1f, 1f, 1f, 0.9f)
            : new Color(0.38f, 0.75f, 1f, 0.44f);

        DrawLine(
            WorldToScreen(checkpoint.Position - side * half),
            WorldToScreen(checkpoint.Position + side * half),
            color,
            7f * _view.Zoom);

        DrawString(
            GetThemeDefaultFont(),
            WorldToScreen(checkpoint.Position + side * (half + 26f)),
            checkpoint.Number.ToString(),
            HorizontalAlignment.Center,
            48,
            22,
            new Color(0.92f, 0.96f, 1f, 0.88f));
    }

    private void DrawGarage(RaceCourse course)
    {
        var center = WorldToScreen(course.GaragePosition);
        var size = new Vector2(280, 190) * _view.Zoom;
        DrawRect(new Rect2(center - size * 0.5f, size), new Color(0.1f, 0.08f, 0.13f, 0.86f));
        DrawRect(
            new Rect2(center - size * 0.5f, size),
            new Color(0.85f, 0.55f, 1f, 0.45f),
            filled: false,
            width: 3f);
        DrawString(
            GetThemeDefaultFont(),
            center + new Vector2(-58, -72) * _view.Zoom,
            "garage",
            HorizontalAlignment.Left,
            -1,
            18,
            new Color(0.95f, 0.86f, 1f));
    }

    private void DrawActor(RaceActor actor)
    {
        var state = actor.State;
        var tuning = actor.Controller.Tuning;
        var forward = Vector2.FromAngle(state.Heading);
        var right = forward.Rotated(Mathf.Pi * 0.5f);
        var position = WorldToScreen(state.Position);

        DrawCircle(position, 34f * _view.Zoom, actor.Controller.Camera.Accent);

        var nose = WorldToScreen(state.Position + forward * 38f);
        var rearLeft = WorldToScreen(state.Position - forward * 30f - right * 20f);
        var rearRight = WorldToScreen(state.Position - forward * 30f + right * 20f);
        var midLeft = WorldToScreen(state.Position + right * 16f);
        var midRight = WorldToScreen(state.Position - right * 16f);

        DrawColoredPolygon([nose, rearRight, rearLeft], tuning.BodyColor);
        DrawLine(midLeft, midRight, new Color(0.06f, 0.07f, 0.08f, 0.8f), 5f * _view.Zoom);
        DrawLine(position, WorldToScreen(state.Position + forward * 48f), new Color(1f, 1f, 1f, 0.72f), 3f);

        if (!actor.Controller.IsOnRoad)
        {
            DrawCircle(position, 44f * _view.Zoom, new Color(0.45f, 0.25f, 0.08f, 0.22f));
        }

        DrawString(
            GetThemeDefaultFont(),
            position + new Vector2(-30, -48) * _view.Zoom,
            actor.Name,
            HorizontalAlignment.Left,
            -1,
            15,
            new Color(0.94f, 0.98f, 0.94f));
    }

    private void DrawMiniMap(RaceCourse course)
    {
        var mapRect = new Rect2(GetViewportRect().Size.X - 210, 18, 188, 136);
        DrawRect(mapRect, new Color(0.02f, 0.04f, 0.04f, 0.78f));
        DrawRect(mapRect, new Color(1f, 1f, 1f, 0.24f), filled: false, width: 1f);

        Vector2 M(Vector2 world)
        {
            var relative = (world - course.Bounds.Position) / course.Bounds.Size;
            return mapRect.Position + relative * mapRect.Size;
        }

        for (var index = 0; index < course.CenterLine.Count; index++)
        {
            DrawLine(
                M(course.CenterLine[index]),
                M(course.CenterLine[(index + 1) % course.CenterLine.Count]),
                new Color(0.72f, 0.76f, 0.72f),
                5f);
        }

        foreach (var actor in _actors.Where(actor => actor.Name != "Garage"))
        {
            DrawCircle(M(actor.State.Position), 5f, actor.Controller.Tuning.BodyColor);
        }
    }

    private Vector2 WorldToScreen(Vector2 world)
        => GetViewportRect().Size * 0.5f + (world - _view.Center) * _view.Zoom;

    private sealed record RaceActor(
        string Name,
        Node Node,
        VehicleState State,
        VehicleController Controller,
        VehicleHudModel Hud,
        IReadOnlyList<string> Groups);
}
