namespace ContextsExample.Game;

public interface ICarModifier
{
    string Label { get; }

    void Apply(VehicleTuning tuning);
}
