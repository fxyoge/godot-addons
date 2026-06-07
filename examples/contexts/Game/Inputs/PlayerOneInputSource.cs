using Godot;

namespace ContextsExample.Game;

public sealed class PlayerOneInputSource : IInputSource
{
    public string Label { get; } = "WASD driver";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => Keyboard(Key.A, Key.D, Key.W, Key.S);

    private static DriveCommand Keyboard(Key left, Key right, Key throttle, Key brake)
    {
        var drive = 0f;
        if (Input.IsKeyPressed(throttle))
        {
            drive += 1f;
        }

        if (Input.IsKeyPressed(brake))
        {
            drive -= 0.72f;
        }

        var steer = 0f;
        if (Input.IsKeyPressed(left))
        {
            steer -= 1f;
        }

        if (Input.IsKeyPressed(right))
        {
            steer += 1f;
        }

        return new DriveCommand(drive, steer);
    }
}
