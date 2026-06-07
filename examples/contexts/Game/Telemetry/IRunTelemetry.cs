namespace ContextsExample.Game;

public interface IRunTelemetry
{
    string Id { get; }

    string Label { get; }

    int Laps { get; }

    int CheckpointIndex { get; }

    void Advance(VehicleState state, ITrackSession track);
}
