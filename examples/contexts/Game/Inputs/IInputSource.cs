namespace ContextsExample.Game;

public interface IInputSource
{
    string Label { get; }

    DriveCommand Read(VehicleState state, ITrackSession track);
}
