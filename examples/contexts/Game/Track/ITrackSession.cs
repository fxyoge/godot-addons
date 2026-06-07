namespace ContextsExample.Game;

public interface ITrackSession
{
    string Id { get; }

    RaceCourse Course { get; }
}
