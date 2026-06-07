namespace ContextsExample.Game;

public sealed class PlayerRunTelemetry : IRunTelemetry
{
    public string Id { get; } = ShortIds.New();

    public string Label { get; } = "live lap timing";

    public int Laps { get; private set; }

    public int CheckpointIndex { get; private set; } = 1;

    public void Advance(VehicleState state, ITrackSession track)
    {
        var checkpoint = track.Course.Checkpoints[CheckpointIndex];
        if (state.Position.DistanceTo(checkpoint.Position) > 118f)
        {
            return;
        }

        CheckpointIndex = (CheckpointIndex + 1) % track.Course.Checkpoints.Count;
        if (CheckpointIndex == 0)
        {
            Laps++;
        }
    }
}
