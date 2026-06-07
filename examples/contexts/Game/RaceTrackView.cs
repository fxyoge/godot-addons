using System.Collections.Generic;
using System.Linq;
using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class RaceTrackView : Control
{
    private readonly List<VehicleActor> _actors = new();
    private ITrackSession? _track;
    private CameraView _view = new(Vector2.Zero, 1f);

    public override void _Ready()
    {
        _track = this.GetRequiredService<ITrackSession>();
        SetProcess(false);
    }

    public void SetActors(IEnumerable<VehicleActor> actors)
    {
        _actors.Clear();
        _actors.AddRange(actors);
    }

    public void SetView(CameraView view)
    {
        _view = view;
        foreach (var actor in _actors)
        {
            actor.ApplyView(view, GetViewportRect().Size);
        }

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
        DrawMiniMap(_track.Course);
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

        foreach (var actor in _actors.Where(actor => !actor.UseGarageStart))
        {
            DrawCircle(M(actor.State.Position), 5f, actor.Controller.Tuning.BodyColor);
        }
    }

    private Vector2 WorldToScreen(Vector2 world)
        => GetViewportRect().Size * 0.5f + (world - _view.Center) * _view.Zoom;
}
