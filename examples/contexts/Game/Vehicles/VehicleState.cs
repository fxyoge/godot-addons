using Godot;

namespace ContextsExample.Game;

public sealed class VehicleState
{
    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public float Heading { get; set; }
}
