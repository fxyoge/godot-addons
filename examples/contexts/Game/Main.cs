using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class Main : Control
{
    private static readonly ServiceProbe[] ServiceProbes =
    [
        new("controller", typeof(VehicleController)),
        new("input", typeof(IInputSource)),
        new("telemetry", typeof(IRunTelemetry)),
        new("track", typeof(ITrackSession)),
        new("surface", typeof(ITrackSurface)),
        new("modifiers", typeof(IEnumerable<ICarModifier>)),
        new("camera", typeof(ICameraRig)),
    ];

    private readonly List<RaceActor> _actors = new();
    private VBoxContainer? _inspectorRoot;
    private RichTextLabel? _contextSummary;
    private GridContainer? _matrix;
    private RichTextLabel? _details;
    private ServiceResolutionDiagnosticsSnapshot? _diagnostics;
    private string _selectedActorName = "Player";
    private Type _selectedServiceType = typeof(VehicleController);
    private ITrackSession? _track;
    private CameraView _view = new(Vector2.Zero, 1f);

    public override void _Ready()
    {
        BuildInspectorOverlay();

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
        CaptureDiagnostics();
        SetProcess(true);
        UpdateInspector();
    }

    public override void _Process(double delta)
    {
        foreach (var actor in _actors)
        {
            actor.Controller.Tick(actor.State, delta);
        }

        var focus = _actors[0];
        _view = focus.Controller.GetCameraView(focus.State, GetViewportRect().Size, delta);
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

    private void BuildInspectorOverlay()
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 1,
            OffsetLeft = 12,
            OffsetTop = 12,
            OffsetRight = 690,
            OffsetBottom = -12,
        };
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        margin.AddChild(scroll);

        _inspectorRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(_inspectorRoot);

        var title = new Label
        {
            Text = "CONTEXTUAL DI INSPECTOR",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        _inspectorRoot.AddChild(title);

        _contextSummary = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _contextSummary.AddThemeFontSizeOverride("normal_font_size", 12);
        _inspectorRoot.AddChild(_contextSummary);

        _matrix = new GridContainer
        {
            Columns = _actors.Count + 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _inspectorRoot.AddChild(_matrix);

        _details = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _details.AddThemeFontSizeOverride("normal_font_size", 12);
        _inspectorRoot.AddChild(_details);
    }

    private void CaptureDiagnostics()
    {
        var gameServices = GetTree().Root.GetNode<GameServices>("GameServices");
        gameServices.ClearDiagnostics();

        foreach (var actor in _actors)
        {
            foreach (var probe in ServiceProbes)
            {
                _ = gameServices.GetRequiredService(actor.Node, probe.Type);
            }
        }

        _diagnostics = gameServices.CreateDiagnosticsSnapshot();
    }

    private void UpdateInspector()
    {
        if (_contextSummary is null || _matrix is null || _details is null || _diagnostics is null)
        {
            return;
        }

        _contextSummary.Text = BuildContextSummaryText();
        RebuildServiceMatrix(_diagnostics);
        _details.Text = BuildDetailsText(_diagnostics);
    }

    private string BuildContextSummaryText()
    {
        var text = new StringBuilder();
        text.AppendLine("CONTEXT NODES");
        foreach (var actor in _actors)
        {
            text.AppendLine($"{actor.Name,-7} {string.Join(", ", actor.Groups)}");
        }

        return text.ToString();
    }

    private void RebuildServiceMatrix(ServiceResolutionDiagnosticsSnapshot diagnostics)
    {
        if (_matrix is null)
        {
            return;
        }

        ClearChildren(_matrix);
        _matrix.Columns = _actors.Count + 1;

        _matrix.AddChild(CreateHeaderLabel("service"));
        foreach (var actor in _actors)
        {
            _matrix.AddChild(CreateHeaderLabel(actor.Name));
        }

        foreach (var probe in ServiceProbes)
        {
            _matrix.AddChild(CreateHeaderLabel(probe.Label));
            foreach (var actor in _actors)
            {
                var trace = FindTrace(diagnostics, actor, probe.Type);
                _matrix.AddChild(CreateMatrixButton(actor, probe, trace));
            }
        }
    }

    private string BuildDetailsText(ServiceResolutionDiagnosticsSnapshot diagnostics)
    {
        var actor = _actors.FirstOrDefault(actor => actor.Name == _selectedActorName) ?? _actors[0];
        var probe = ServiceProbes.FirstOrDefault(probe => probe.Type == _selectedServiceType) ?? ServiceProbes[0];
        var trace = FindTrace(diagnostics, actor, probe.Type);
        var text = new StringBuilder();

        text.AppendLine();
        text.AppendLine($"SELECTED: {actor.Name} / {probe.Label}");
        text.AppendLine($"groups: {string.Join(", ", actor.Groups)}");
        text.AppendLine();

        if (trace is null)
        {
            text.AppendLine("missing trace");
            return text.ToString();
        }

        text.AppendLine("RESOLUTION GRAPH");
        AppendNode(text, trace.Root, 0);
        text.AppendLine();

        text.AppendLine("CONTEXTUAL RULE DECISIONS");
        foreach (var registration in trace.Root.ContextualRegistrations)
        {
            var state = registration.Matches ? "match" : "skip ";
            var partition = registration.Partition is null ? "" : $" partition={Shorten(registration.Partition, 56)}";
            text.AppendLine(
                $"  {state} {ShortType(registration.ImplementationType),-24} rule={registration.Rule}{partition}");
        }

        if (trace.Root.ContextualRegistrations.Length == 0)
        {
            text.AppendLine("  no contextual registrations for this service type");
        }

        text.AppendLine();
        text.AppendLine("INSTANCE LEDGER");
        foreach (var instance in diagnostics.Instances
            .OrderBy(instance => instance.Source)
            .ThenBy(instance => instance.Partition ?? "")
            .ThenBy(instance => instance.ImplementationType.Name))
        {
            var lifetime = instance.Lifetime?.ToString() ?? "n/a";
            var partition = instance.Partition is null ? "root/no partition" : Shorten(instance.Partition, 56);
            text.AppendLine(
                $"{instance.Id,-28} {instance.Source,-10} {lifetime,-9} {ShortType(instance.ServiceType)} -> {ShortType(instance.ImplementationType)}");
            text.AppendLine($"  {partition}");
        }

        return text.ToString();
    }

    private static Label CreateHeaderLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    private Button CreateMatrixButton(RaceActor actor, ServiceProbe probe, ServiceResolutionTrace? trace)
    {
        var selected = actor.Name == _selectedActorName && probe.Type == _selectedServiceType;
        var button = new Button
        {
            Text = Shorten(DescribeCell(trace), 18),
            ToggleMode = true,
            ButtonPressed = selected,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = trace is null ? "No trace" : DescribeCell(trace),
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += () =>
        {
            _selectedActorName = actor.Name;
            _selectedServiceType = probe.Type;
            UpdateInspector();
        };
        return button;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void AppendNode(StringBuilder text, ServiceResolutionTraceNode node, int depth)
    {
        var prefix = new string(' ', depth * 2);
        text.AppendLine(
            $"{prefix}{ShortType(node.ServiceType)} -> {DescribeNodeResult(node)}");

        foreach (var item in node.Items)
        {
            text.AppendLine(
                $"{prefix}  item {ShortType(item.ServiceType)} -> {ShortType(item.ImplementationType)} {item.InstanceId}");
        }

        foreach (var dependency in node.Dependencies)
        {
            AppendNode(text, dependency, depth + 1);
        }
    }

    private ServiceResolutionTrace? FindTrace(
        ServiceResolutionDiagnosticsSnapshot diagnostics,
        RaceActor actor,
        Type serviceType)
    {
        var groups = actor.Groups.OrderBy(group => group).ToImmutableArray();
        return diagnostics.Traces
            .LastOrDefault(trace => trace.ServiceType == serviceType
                && trace.ContextGroups.SequenceEqual(groups));
    }

    private static string DescribeCell(ServiceResolutionTrace? trace)
        => trace is null ? "no trace" : DescribeNodeResult(trace.Root);

    private static string DescribeNodeResult(ServiceResolutionTraceNode node)
    {
        if (node.Source == "enumerable")
        {
            return string.Join(" + ", node.Items.Select(item => ShortType(item.ImplementationType)));
        }

        var cache = node.CacheHit is null ? "" : node.CacheHit.Value ? " hit" : " new";
        return $"{ShortType(node.ImplementationType)} {node.InstanceId}{cache}";
    }

    private static string ShortType(Type? type)
    {
        if (type is null)
        {
            return "null";
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return $"IEnumerable<{ShortType(type.GetGenericArguments()[0])}>";
        }

        if (type.IsArray)
        {
            return $"{ShortType(type.GetElementType())}[]";
        }

        return type.Name;
    }

    private static string Shorten(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";

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

    private sealed record ServiceProbe(string Label, Type Type);
}
