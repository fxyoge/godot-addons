using Godot;

namespace ContextsExample.Game;

public sealed class ForestTrackSurface : ITrackSurface
{
    public string Label { get; } = "forest asphalt + grass runoff";

    public float SpeedMultiplier(Vector2 position, RaceCourse course)
        => course.IsOnRoad(position) ? 1f : 0.48f;

    public float GripMultiplier(Vector2 position, RaceCourse course)
        => course.IsOnRoad(position) ? 1f : 0.62f;
}
