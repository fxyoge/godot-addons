using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextsExample.Game;

public sealed class VehicleState
{
    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public float Heading { get; set; }
}

public readonly record struct DriveCommand(float Throttle, float Steering);

public readonly record struct CameraView(Vector2 Center, float Zoom);

public sealed class VehicleTuning
{
    public float Acceleration { get; set; } = 780f;

    public float MaxSpeed { get; set; } = 520f;

    public float Drag { get; set; } = 0.965f;

    public float TurnRate { get; set; } = 3.2f;

    public Color BodyColor { get; set; } = new(0.96f, 0.25f, 0.17f);
}

public sealed class RaceCourse
{
    public RaceCourse(Rect2 bounds, float roadWidth, IReadOnlyList<Vector2> centerLine)
    {
        Bounds = bounds;
        RoadWidth = roadWidth;
        CenterLine = centerLine;
        Checkpoints = centerLine
            .Select((point, index) => new Checkpoint(index + 1, point))
            .ToArray();
    }

    public Rect2 Bounds { get; }

    public float RoadWidth { get; }

    public IReadOnlyList<Vector2> CenterLine { get; }

    public IReadOnlyList<Checkpoint> Checkpoints { get; }

    public Vector2 GaragePosition { get; } = new(2780, 1860);

    public Vector2 GetStartPosition(int slot)
    {
        var origin = CenterLine[0];
        return slot switch
        {
            1 => origin + new Vector2(-30, -42),
            2 => origin + new Vector2(-30, 42),
            _ => origin + new Vector2(-96, 0),
        };
    }

    public float DistanceToRoad(Vector2 point)
    {
        var nearest = float.MaxValue;
        for (var index = 0; index < CenterLine.Count; index++)
        {
            var start = CenterLine[index];
            var end = CenterLine[(index + 1) % CenterLine.Count];
            nearest = Math.Min(nearest, DistanceToSegment(point, start, end));
        }

        return nearest;
    }

    public bool IsOnRoad(Vector2 point) => DistanceToRoad(point) <= RoadWidth * 0.5f;

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceTo(start);
        }

        var t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * t);
    }
}

public readonly record struct Checkpoint(int Number, Vector2 Position);

public interface ITrackSession
{
    string Id { get; }

    RaceCourse Course { get; }
}

public sealed class TrackSession : ITrackSession
{
    public string Id { get; } = ShortIds.New();

    public RaceCourse Course { get; } = new(
        new Rect2(120, 120, 3060, 2100),
        210f,
        [
            new(440, 1510),
            new(620, 760),
            new(1160, 420),
            new(1800, 520),
            new(2500, 360),
            new(2920, 850),
            new(2660, 1390),
            new(2120, 1620),
            new(1710, 1210),
            new(1300, 1730),
            new(780, 1840),
        ]);
}

public interface ITrackSurface
{
    string Label { get; }

    float SpeedMultiplier(Vector2 position, RaceCourse course);

    float GripMultiplier(Vector2 position, RaceCourse course);
}

public sealed class ForestTrackSurface : ITrackSurface
{
    public string Label { get; } = "forest asphalt + grass runoff";

    public float SpeedMultiplier(Vector2 position, RaceCourse course)
        => course.IsOnRoad(position) ? 1f : 0.48f;

    public float GripMultiplier(Vector2 position, RaceCourse course)
        => course.IsOnRoad(position) ? 1f : 0.62f;
}

public interface IInputSource
{
    string Label { get; }

    DriveCommand Read(VehicleState state, ITrackSession track);
}

public sealed class PlayerOneInputSource : IInputSource
{
    public string Label { get; } = "WASD driver";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => Keyboard(Key.A, Key.D, Key.W, Key.S);

    private static DriveCommand Keyboard(Key left, Key right, Key throttle, Key brake)
    {
        var drive = 0f;
        if (Input.IsKeyPressed(throttle))
        {
            drive += 1f;
        }

        if (Input.IsKeyPressed(brake))
        {
            drive -= 0.72f;
        }

        var steer = 0f;
        if (Input.IsKeyPressed(left))
        {
            steer -= 1f;
        }

        if (Input.IsKeyPressed(right))
        {
            steer += 1f;
        }

        return new DriveCommand(drive, steer);
    }
}

public sealed class RivalInputSource : IInputSource
{
    private int _targetIndex = 1;

    public string Label { get; } = "context AI rival";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => ChaseCourse(state, track.Course, 0.92f, ref _targetIndex);

    internal static DriveCommand ChaseCourse(
        VehicleState state,
        RaceCourse course,
        float throttle,
        ref int targetIndex)
    {
        var target = course.CenterLine[targetIndex];
        if (state.Position.DistanceTo(target) < 150f)
        {
            targetIndex = (targetIndex + 1) % course.CenterLine.Count;
            target = course.CenterLine[targetIndex];
        }

        var desired = state.Position.DirectionTo(target).Angle();
        var turn = Mathf.Wrap(desired - state.Heading, -Mathf.Pi, Mathf.Pi);
        var steering = Mathf.Clamp(turn * 1.85f, -1f, 1f);
        var speedLimit = Math.Abs(turn) > 0.8f ? throttle * 0.58f : throttle;
        return new DriveCommand(speedLimit, steering);
    }
}

public sealed class ReplayInputSource : IInputSource
{
    private int _targetIndex = 1;

    public string Label { get; } = "ghost racing line";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => RivalInputSource.ChaseCourse(state, track.Course, 0.82f, ref _targetIndex);
}

public sealed class PreviewSpinInputSource : IInputSource
{
    public string Label { get; } = "garage turntable";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => new(0f, 1f);
}

public interface IRunTelemetry
{
    string Id { get; }

    string Label { get; }

    int Laps { get; }

    int CheckpointIndex { get; }

    void Advance(VehicleState state, ITrackSession track);
}

public sealed class PlayerRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "live lap timing";

    public int Laps { get; private set; }

    public int CheckpointIndex { get; private set; } = 1;

    public void Advance(VehicleState state, ITrackSession track)
    {
        var checkpoint = track.Course.Checkpoints[CheckpointIndex];
        if (state.Position.DistanceTo(checkpoint.Position) > 118f)
        {
            return;
        }

        CheckpointIndex = (CheckpointIndex + 1) % track.Course.Checkpoints.Count;
        if (CheckpointIndex == 0)
        {
            Laps++;
        }
    }
}

public sealed class ReplayRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "read-only ghost lap";

    public int Laps { get; private set; } = 2;

    public int CheckpointIndex { get; private set; } = 1;

    public void Advance(VehicleState state, ITrackSession track)
    {
        var checkpoint = track.Course.Checkpoints[CheckpointIndex];
        if (state.Position.DistanceTo(checkpoint.Position) <= 126f)
        {
            CheckpointIndex = (CheckpointIndex + 1) % track.Course.Checkpoints.Count;
        }
    }
}

public sealed class PreviewRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "showroom only";

    public int Laps => 0;

    public int CheckpointIndex => 0;

    public void Advance(VehicleState state, ITrackSession track)
    {
    }
}

public interface ICarModifier
{
    string Label { get; }

    void Apply(VehicleTuning tuning);
}

public sealed class BaseHandlingModifier : ICarModifier
{
    public string Label { get; } = "base race car";

    public void Apply(VehicleTuning tuning)
    {
    }
}

public sealed class GrandPrixSurfaceModifier : ICarModifier
{
    public string Label { get; } = "forest course tune";

    public void Apply(VehicleTuning tuning)
    {
        tuning.Drag = 0.972f;
        tuning.TurnRate = 3.45f;
    }
}

public sealed class PlayerPaintModifier : ICarModifier
{
    public string Label { get; } = "player red livery";

    public void Apply(VehicleTuning tuning)
        => tuning.BodyColor = new Color(0.95f, 0.16f, 0.12f);
}

public sealed class RivalPaintModifier : ICarModifier
{
    public string Label { get; } = "rival blue livery";

    public void Apply(VehicleTuning tuning)
    {
        tuning.MaxSpeed *= 0.96f;
        tuning.BodyColor = new Color(0.14f, 0.42f, 0.96f);
    }
}

public sealed class ReplayGhostModifier : ICarModifier
{
    public string Label { get; } = "transparent replay ghost";

    public void Apply(VehicleTuning tuning)
    {
        tuning.MaxSpeed *= 0.9f;
        tuning.BodyColor = new Color(1f, 0.83f, 0.18f, 0.58f);
    }
}

public sealed class PreviewShowroomModifier : ICarModifier
{
    public string Label { get; } = "garage display tune";

    public void Apply(VehicleTuning tuning)
    {
        tuning.BodyColor = new Color(0.86f, 0.32f, 0.92f);
        tuning.MaxSpeed = 0f;
    }
}

public interface ICameraRig
{
    string Id { get; }

    string Label { get; }

    Color Accent { get; }

    CameraView GetView(VehicleState focus, ITrackSession track, Vector2 viewportSize);
}

public sealed class RuntimeCameraRig : ICameraRig
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "player chase camera";

    public Color Accent { get; } = new(1f, 1f, 1f, 0.28f);

    public CameraView GetView(VehicleState focus, ITrackSession track, Vector2 viewportSize)
    {
        const float zoom = 0.72f;
        var speed = focus.Velocity.Length();
        var lead = Mathf.Clamp(speed * 0.24f, 72f, 150f);
        var center = focus.Position + Vector2.FromAngle(focus.Heading) * lead;
        return CameraBounds.ClampToCourse(center, zoom, viewportSize, track.Course.Bounds);
    }
}

public sealed class ReplayCameraRig : ICameraRig
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "broadcast replay camera";

    public Color Accent { get; } = new(1f, 0.86f, 0.2f, 0.42f);

    public CameraView GetView(VehicleState focus, ITrackSession track, Vector2 viewportSize)
    {
        const float zoom = 0.64f;
        var center = focus.Position + Vector2.FromAngle(focus.Heading) * 180f;
        return CameraBounds.ClampToCourse(center, zoom, viewportSize, track.Course.Bounds);
    }
}

public sealed class PreviewCameraRig : ICameraRig
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "garage camera";

    public Color Accent { get; } = new(0.9f, 0.38f, 1f, 0.45f);

    public CameraView GetView(VehicleState focus, ITrackSession track, Vector2 viewportSize)
    {
        const float zoom = 1.2f;
        return CameraBounds.ClampToCourse(track.Course.GaragePosition, zoom, viewportSize, track.Course.Bounds);
    }
}

internal static class CameraBounds
{
    public static CameraView ClampToCourse(Vector2 center, float zoom, Vector2 viewportSize, Rect2 bounds)
    {
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f || zoom <= 0f)
        {
            return new CameraView(center, zoom);
        }

        var halfVisibleWorld = viewportSize / (zoom * 2f);
        var min = bounds.Position + halfVisibleWorld;
        var max = bounds.End - halfVisibleWorld;

        if (min.X > max.X)
        {
            center.X = bounds.GetCenter().X;
        }
        else
        {
            center.X = Mathf.Clamp(center.X, min.X, max.X);
        }

        if (min.Y > max.Y)
        {
            center.Y = bounds.GetCenter().Y;
        }
        else
        {
            center.Y = Mathf.Clamp(center.Y, min.Y, max.Y);
        }

        return new CameraView(center, zoom);
    }
}

public sealed class VehicleController
{
    private readonly IInputSource _input;
    private readonly IRunTelemetry _telemetry;
    private readonly ITrackSession _track;
    private readonly ITrackSurface _surface;
    private readonly ICarModifier[] _modifiers;
    private CameraView? _cameraView;

    public VehicleController(
        IInputSource input,
        IRunTelemetry telemetry,
        ITrackSession track,
        ITrackSurface surface,
        IEnumerable<ICarModifier> modifiers,
        ICameraRig camera)
    {
        _input = input;
        _telemetry = telemetry;
        _track = track;
        _surface = surface;
        _modifiers = modifiers.ToArray();
        Camera = camera;

        Tuning = new VehicleTuning();
        foreach (var modifier in _modifiers)
        {
            modifier.Apply(Tuning);
        }
    }

    public VehicleTuning Tuning { get; }

    public ICameraRig Camera { get; }

    public string InputLabel => _input.Label;

    public string TelemetryLabel => $"{_telemetry.Label}/{_telemetry.Id}";

    public string TrackLabel => $"track/{_track.Id}";

    public string SurfaceLabel => _surface.Label;

    public string ModifierLabel => string.Join(", ", _modifiers.Select(modifier => modifier.Label));

    public int Laps => _telemetry.Laps;

    public int Checkpoint => _telemetry.CheckpointIndex + 1;

    public float Speed => LastSpeed;

    public bool IsOnRoad { get; private set; }

    private float LastSpeed { get; set; }

    public CameraView GetCameraView(VehicleState state, Vector2 viewportSize, double delta = 0)
    {
        var target = Camera.GetView(state, _track, viewportSize);
        if (_cameraView is not { } current || delta <= 0)
        {
            _cameraView = target;
            return target;
        }

        var smoothing = 1f - Mathf.Exp((float)delta * -7.5f);
        var center = current.Center.Lerp(target.Center, smoothing);
        var view = CameraBounds.ClampToCourse(center, target.Zoom, viewportSize, _track.Course.Bounds);
        _cameraView = view;
        return view;
    }

    public void Tick(VehicleState state, double delta)
    {
        var seconds = (float)delta;
        var course = _track.Course;
        var command = _input.Read(state, _track);
        var speed = state.Velocity.Length();
        var speedRatio = Tuning.MaxSpeed <= 0 ? 0.35f : Mathf.Clamp(speed / Tuning.MaxSpeed, 0.15f, 1f);
        var grip = _surface.GripMultiplier(state.Position, course);

        state.Heading += command.Steering * Tuning.TurnRate * grip * seconds * speedRatio;

        var forward = Vector2.FromAngle(state.Heading);
        state.Velocity += forward * command.Throttle * Tuning.Acceleration * seconds;

        var maxSpeed = Tuning.MaxSpeed * _surface.SpeedMultiplier(state.Position, course);
        if (state.Velocity.Length() > maxSpeed)
        {
            state.Velocity = state.Velocity.Normalized() * maxSpeed;
        }

        state.Position += state.Velocity * seconds;
        state.Velocity *= Mathf.Pow(Tuning.Drag, seconds * 8f);
        ClampToWorld(state, course.Bounds);

        IsOnRoad = course.IsOnRoad(state.Position);
        LastSpeed = state.Velocity.Length();
        _telemetry.Advance(state, _track);
    }

    private static void ClampToWorld(VehicleState state, Rect2 bounds)
    {
        state.Position = new Vector2(
            Mathf.Clamp(state.Position.X, bounds.Position.X, bounds.End.X),
            Mathf.Clamp(state.Position.Y, bounds.Position.Y, bounds.End.Y));
    }
}

public sealed class VehicleHudModel
{
    private readonly VehicleController _controller;

    public VehicleHudModel(VehicleController controller)
    {
        _controller = controller;
    }

    public string BuildLine(string name)
        => $"{name}: lap {_controller.Laps} cp {_controller.Checkpoint} "
            + $"speed {Mathf.RoundToInt(_controller.Speed)} "
            + $"| {_controller.InputLabel} | {_controller.Camera.Label}/{_controller.Camera.Id}";
}

internal static class ShortIds
{
    public static string New() => Guid.NewGuid().ToString("N")[..6];
}
