using System;
using Godot;

namespace ContextsExample.Game;

public sealed class RivalInputSource : IInputSource
{
    private int _targetIndex = 1;

    public string Label { get; } = "context AI rival";

    public DriveCommand Read(VehicleState state, ITrackSession track)
        => ChaseCourse(state, track.Course, 0.92f, ref _targetIndex);

    internal static DriveCommand ChaseCourse(
        VehicleState state,
        RaceCourse course,
        float throttle,
        ref int targetIndex)
    {
        var target = course.CenterLine[targetIndex];
        if (state.Position.DistanceTo(target) < 150f)
        {
            targetIndex = (targetIndex + 1) % course.CenterLine.Count;
            target = course.CenterLine[targetIndex];
        }

        var desired = state.Position.DirectionTo(target).Angle();
        var turn = Mathf.Wrap(desired - state.Heading, -Mathf.Pi, Mathf.Pi);
        var steering = Mathf.Clamp(turn * 1.85f, -1f, 1f);
        var speedLimit = Math.Abs(turn) > 0.8f ? throttle * 0.58f : throttle;
        return new DriveCommand(speedLimit, steering);
    }
}
