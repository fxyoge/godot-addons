using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextsExample.Game;

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
