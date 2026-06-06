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

## Resolve Services

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
