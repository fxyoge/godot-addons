namespace ContextsExample.Game;

public sealed class BaseHandlingModifier : ICarModifier
{
    public string Label { get; } = "base race car";

    public void Apply(VehicleTuning tuning)
    {
    }
}
