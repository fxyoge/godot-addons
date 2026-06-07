using Fxyoge.DependencyInjection.Configuration;

namespace ConfigExample.Game;

public sealed class AudioOptions
{
    public float MasterVolume { get; set; }

    public bool Muted { get; set; }
}

public sealed class InputOptions
{
    public InputActionBindings Jump { get; set; } = new();
}

public sealed class GameplayOptions
{
    public string Difficulty { get; set; } = string.Empty;

    public bool ShowDamageNumbers { get; set; }

    public float SpawnRate { get; set; }
}

public sealed class ProjectDefaultsOptions
{
    public string GameTitle { get; set; } = string.Empty;
}
