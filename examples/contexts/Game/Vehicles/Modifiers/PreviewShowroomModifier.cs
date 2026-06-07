using Godot;

namespace ContextsExample.Game;

public sealed class PreviewShowroomModifier : ICarModifier
{
    public string Label { get; } = "garage display tune";

    public void Apply(VehicleTuning tuning)
    {
        tuning.BodyColor = new Color(0.86f, 0.32f, 0.92f);
        tuning.MaxSpeed = 0f;
    }
}
