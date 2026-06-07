using Godot;

namespace Fxyoge.DependencyInjection;

public static class NodeServiceExtensions
{
    public static T GetRequiredService<T>(this Node node)
        where T : notnull
        => GetGameServices(node).GetRequiredService<T>(node);

    public static T? GetService<T>(this Node node)
        where T : class
        => GetGameServices(node).GetService<T>(node);

    public static void DisposeContext(this Node node)
        => GetGameServices(node).DisposeContext(node);

    private static GameServices GetGameServices(Node node)
    {
        if (node is GameServices gameServices)
        {
            return gameServices;
        }

        return node
            .GetTree()
            .Root
            .GetNode<GameServices>("GameServices");
    }
}
