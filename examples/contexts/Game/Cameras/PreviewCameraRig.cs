using Godot;

namespace ContextsExample.Game;

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
