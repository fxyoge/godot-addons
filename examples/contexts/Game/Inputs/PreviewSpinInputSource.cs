namespace ContextsExample.Game;

public sealed class PreviewSpinInputSource : IInputSource
{
    public string Label { get; } = "garage turntable";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => new(0f, 1f);
}
