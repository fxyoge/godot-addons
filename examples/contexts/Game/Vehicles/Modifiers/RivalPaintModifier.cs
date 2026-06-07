using Godot;

namespace ContextsExample.Game;

public sealed class RivalPaintModifier : ICarModifier
{
    public string Label { get; } = "rival blue livery";

    public void Apply(VehicleTuning tuning)
    {
        tuning.MaxSpeed *= 0.96f;
        tuning.BodyColor = new Color(0.14f, 0.42f, 0.96f);
    }
}
