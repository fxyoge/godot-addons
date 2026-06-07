namespace ContextsExample.Game;

public sealed class ReplayInputSource : IInputSource
{
    private int _targetIndex = 1;

    public string Label { get; } = "ghost racing line";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => RivalInputSource.ChaseCourse(state, track.Course, 0.82f, ref _targetIndex);
}
