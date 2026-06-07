using Godot;

namespace ContextsExample.Game;

public interface ITrackSurface
{
    string Label { get; }

    float SpeedMultiplier(Vector2 position, RaceCourse course);

    float GripMultiplier(Vector2 position, RaceCourse course);
}
