using Godot;

namespace ContextsExample.Game;

public sealed class TrackSession : ITrackSession
{
    public string Id { get; } = ShortIds.New();

    public RaceCourse Course { get; } = new(
        new Rect2(120, 120, 3060, 2100),
        210f,
        [
            new(440, 1510),
            new(620, 760),
            new(1160, 420),
            new(1800, 520),
            new(2500, 360),
            new(2920, 850),
            new(2660, 1390),
            new(2120, 1620),
            new(1710, 1210),
            new(1300, 1730),
            new(780, 1840),
        ]);
}
