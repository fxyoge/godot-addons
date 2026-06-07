using Godot;

namespace ContextsExample.Game;

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
