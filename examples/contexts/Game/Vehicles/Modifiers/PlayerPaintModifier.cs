using Godot;

namespace ContextsExample.Game;

public sealed class PlayerPaintModifier : ICarModifier
{
    public string Label { get; } = "player red livery";

    public void Apply(VehicleTuning tuning)
        => tuning.BodyColor = new Color(0.95f, 0.16f, 0.12f);
}
