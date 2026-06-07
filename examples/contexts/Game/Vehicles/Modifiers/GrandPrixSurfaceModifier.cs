namespace ContextsExample.Game;

public sealed class GrandPrixSurfaceModifier : ICarModifier
{
    public string Label { get; } = "forest course tune";

    public void Apply(VehicleTuning tuning)
    {
        tuning.Drag = 0.972f;
        tuning.TurnRate = 3.45f;
    }
}
