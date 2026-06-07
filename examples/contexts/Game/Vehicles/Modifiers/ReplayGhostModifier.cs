using Godot;

namespace ContextsExample.Game;

public sealed class ReplayGhostModifier : ICarModifier
{
    public string Label { get; } = "transparent replay ghost";

    public void Apply(VehicleTuning tuning)
    {
        tuning.MaxSpeed *= 0.9f;
        tuning.BodyColor = new Color(1f, 0.83f, 0.18f, 0.58f);
    }
}
