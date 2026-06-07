# Fxyoge Dependency Injection

ASP.NET-style dependency injection for Godot C# projects.

## Install

Recommended: Use GodotEnv to install the addon into your project.

Add `fxy_di` to `addons.jsonc` (`godotenv addons init` to create an empty one).

```jsonc
{
  "addons": {
    "fxy_di": {
      "url": "https://github.com/fxyoge/godot-addons.git",
      "source": "remote",
      "checkout": "main",
      "subfolder": "addons/fxy_di"
    }
  }
}
```

Install packages.

```sh
godotenv addons install
dotnet package add Microsoft.Extensions.DependencyInjection
```

Add the `GameServices` autoload in `project.godot`.

```ini
[autoload]

GameServices="*res://addons/fxy_di/GameServices.tscn"
```

## Configure Services

Add one or more startup classes anywhere in your project assemblies.

```csharp
using Fxyoge.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

public sealed class GameStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
    }
}
```

`GameServices` scans loaded assemblies, creates every concrete `IStartup`, calls
`ConfigureServices`, and builds a single root `ServiceProvider`.

## Resolve Root Services

Resolve services from the `GameServices` autoload.

```csharp
using Fxyoge.DependencyInjection;
using Godot;

public partial class Main : Node
{
    private IMyService? _myService;

    private IMyService MyService =>
        _myService ??= GetTree()
            .Root
            .GetNode<GameServices>("GameServices")
            .GetRequiredService<IMyService>();
}
```

## Contextual Services

Contextual services resolve from the Godot groups on the node requesting them.

```csharp
public sealed class GameStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
        services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        services.AddContextualScoped<ISettingsStore, ReplaySettingsStore>("replay");

        services.AddTransient<SettingsManager>();
        services.AddContextualScoped<ILevelSession, LevelSession>("level:*");
        services.AddContextualScoped<IPlayerProfile, PlayerProfile>("level:*", "player:*");
    }
}
```

Resolve from any `Node` with the extension methods.

```csharp
public partial class PlayerHud : Control
{
    private SettingsManager? _settings;

    private SettingsManager Settings =>
        _settings ??= this.GetRequiredService<SettingsManager>();
}
```

If the node is in groups `["preview", "level:forest", "player:1"]`,
`SettingsManager` is still a normal transient, but its dependencies are built
through that node context. An `IEnumerable<ISettingsStore>` dependency receives
the normal stores plus the matching `PreviewSettingsStore`.

Contextual rules also determine the default sharing boundary:

```text
"preview"              one shared contextual instance for preview
"!preview"             one shared contextual instance when preview is absent
"level:*"              one instance per matched group, such as level:forest
"level:*", "player:*"  one instance per matched tuple
```

For single-service resolution, the most specific matching contextual
registration wins. For `IEnumerable<T>`, normal services are returned first,
then every matching contextual service.

Contextual singleton registrations share one instance for the registration.
Contextual scoped registrations share by the inferred rule boundary above.
Contextual transient registrations create a new instance per resolution.
