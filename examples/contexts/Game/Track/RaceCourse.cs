using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextsExample.Game;

public sealed class RaceCourse
{
    public RaceCourse(Rect2 bounds, float roadWidth, IReadOnlyList<Vector2> centerLine)
    {
        Bounds = bounds;
        RoadWidth = roadWidth;
        CenterLine = centerLine;
        Checkpoints = centerLine
            .Select((point, index) => new Checkpoint(index + 1, point))
            .ToArray();
    }

    public Rect2 Bounds { get; }

    public float RoadWidth { get; }

    public IReadOnlyList<Vector2> CenterLine { get; }

    public IReadOnlyList<Checkpoint> Checkpoints { get; }

    public Vector2 GaragePosition { get; } = new(2780, 1860);

    public Vector2 GetStartPosition(int slot)
    {
        var origin = CenterLine[0];
        return slot switch
        {
            1 => origin + new Vector2(-30, -42),
            2 => origin + new Vector2(-30, 42),
            _ => origin + new Vector2(-96, 0),
        };
    }

    public float DistanceToRoad(Vector2 point)
    {
        var nearest = float.MaxValue;
        for (var index = 0; index < CenterLine.Count; index++)
        {
            var start = CenterLine[index];
            var end = CenterLine[(index + 1) % CenterLine.Count];
            nearest = Math.Min(nearest, DistanceToSegment(point, start, end));
        }

        return nearest;
    }

    public bool IsOnRoad(Vector2 point) => DistanceToRoad(point) <= RoadWidth * 0.5f;

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceTo(start);
        }

        var t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * t);
    }
}

public readonly record struct Checkpoint(int Number, Vector2 Position);
