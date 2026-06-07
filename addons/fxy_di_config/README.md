# Fxyoge DI Config

Typed writable configuration for Godot C# projects using Fxyoge Dependency Injection.

`fxy_di_config` gives game code a Microsoft-options-shaped read API plus a writable monitor for settings menus. Godot systems such as `InputMap`, `AudioServer`, and `ProjectSettings` are mapped once during DI registration, then consumed as typed options.

## Install

Recommended: use GodotEnv to vendor the addon into your project.

```jsonc
{
  "addons": {
    "fxy_di": {
      "url": "https://github.com/fxyoge/godot-addons.git",
      "source": "remote",
      "checkout": "main",
      "subfolder": "addons/fxy_di"
    },
    "fxy_di_config": {
      "url": "https://github.com/fxyoge/godot-addons.git",
      "source": "remote",
      "checkout": "main",
      "subfolder": "addons/fxy_di_config"
    }
  }
}
```

```sh
godotenv addons install
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.1
dotnet add package Microsoft.Extensions.Configuration --version 8.0.0
dotnet add package Microsoft.Extensions.Configuration.Binder --version 8.0.2
dotnet add package Microsoft.Extensions.Options --version 8.0.2
dotnet add package Microsoft.Extensions.Primitives --version 8.0.0
```

## Configure

```csharp
services.AddUserConfig();

services.AddSettings<AudioOptions>("audio", audio =>
{
    audio.Map(x => x.MasterVolume)
        .WithUi("Master Volume", ConfigUiControl.Slider, 0, 1, 0.01)
        .ToAudioBusVolume("Master", 0.8f);

    audio.Map(x => x.Muted)
        .WithUi("Mute", ConfigUiControl.Toggle)
        .ToAudioBusMute("Master", false);
});
```

Settings are persisted to `user://settings.cfg` by default. Project settings can be used as defaults, with player changes saved in the settings file.

Use small POCOs with mapped scalar properties. Use `InputActionBinding` for key bindings.

The settings file uses Godot `ConfigFile` sections, slash paths, and native values:

```ini
[audio]

master/volume=0.8
master/muted=false

[input]

jump/key_code=74

[gameplay]

difficulty="Hard"
show_damage_numbers=true
camera_sensitivity=0.35
```

## Consume

```csharp
public sealed class SettingsMenu
{
    private readonly ISettingsMonitor<AudioOptions> _audio;

    public SettingsMenu(ISettingsMonitor<AudioOptions> audio)
    {
        _audio = audio;
    }

    public ValueTask SetVolume(float volume)
        => _audio.Update(options => options.MasterVolume = volume);
}
```

Use `CreateSession()` for apply/cancel/reset menu flows.
