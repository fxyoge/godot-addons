using Godot;

namespace ContextsExample.Game;

public sealed class VehicleTuning
{
    public float Acceleration { get; set; } = 780f;

    public float MaxSpeed { get; set; } = 520f;

    public float Drag { get; set; } = 0.965f;

    public float TurnRate { get; set; } = 3.2f;

    public Color BodyColor { get; set; } = new(0.96f, 0.25f, 0.17f);
}
