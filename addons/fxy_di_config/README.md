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
services.AddFxyDiConfig();

services.AddWritableOptions<AudioOptions>("audio", audio =>
{
    audio.Map(x => x.MasterVolume)
        .WithUi("Master Volume", ConfigUiControl.Slider, 0, 1, 0.01)
        .ToAudioBusVolume("Master", 0.8f);

    audio.Map(x => x.Muted)
        .WithUi("Mute", ConfigUiControl.Toggle)
        .ToAudioBusMute("Master", false);
});
```

Settings are persisted to `user://settings.cfg` by default. Project settings are read as defaults and user overrides are written to the user overlay, not to `project.godot`.

Options should be small JSON-serializable POCOs. For Godot input mappings, use `InputActionBinding` instead of storing raw `InputEvent` instances on options.

## Consume

```csharp
public sealed class SettingsMenu
{
    private readonly IWritableOptionsMonitor<AudioOptions> _audio;

    public SettingsMenu(IWritableOptionsMonitor<AudioOptions> audio)
    {
        _audio = audio;
    }

    public ValueTask SetVolume(float volume)
        => _audio.Update(options => options.MasterVolume = volume);
}
```

Use `CreateSession()` for apply/cancel/reset menu flows.
