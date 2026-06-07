namespace ContextsExample.Game;

public sealed class PreviewRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "showroom only";

    public int Laps => 0;

    public int CheckpointIndex => 0;

    public void Advance(VehicleState state, ITrackSession track)
    {
    }
}
