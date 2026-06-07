namespace ContextsExample.Game;

public sealed class ReplayRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "read-only ghost lap";

    public int Laps { get; private set; } = 2;

    public int CheckpointIndex { get; private set; } = 1;

    public void Advance(VehicleState state, ITrackSession track)
    {
        var checkpoint = track.Course.Checkpoints[CheckpointIndex];
        if (state.Position.DistanceTo(checkpoint.Position) <= 126f)
        {
            CheckpointIndex = (CheckpointIndex + 1) % track.Course.Checkpoints.Count;
        }
    }
}
