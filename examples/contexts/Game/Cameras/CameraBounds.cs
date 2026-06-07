using Godot;

namespace ContextsExample.Game;

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

        center.X = min.X > max.X
            ? bounds.GetCenter().X
            : Mathf.Clamp(center.X, min.X, max.X);
        center.Y = min.Y > max.Y
            ? bounds.GetCenter().Y
            : Mathf.Clamp(center.Y, min.Y, max.Y);

        return new CameraView(center, zoom);
    }
}
