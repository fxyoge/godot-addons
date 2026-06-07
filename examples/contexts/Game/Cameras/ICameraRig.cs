using Godot;

namespace ContextsExample.Game;

public interface ICameraRig
{
    string Id { get; }

    string Label { get; }

    Color Accent { get; }

    CameraView GetView(VehicleState focus, ITrackSession track, Vector2 viewportSize);
}
