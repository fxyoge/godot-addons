using Godot;

namespace ContextsExample.Game;

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
