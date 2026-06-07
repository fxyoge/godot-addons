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
        .PersistAs("master/volume")
        .ToRuntime(new GodotAudioBusVolumeBinding("Master", 0.8f), 0.8f);

    audio.Map(x => x.Muted)
        .WithUi("Mute", ConfigUiControl.Toggle)
        .PersistAs("master/muted")
        .ToRuntime(new GodotAudioBusMuteBinding("Master", false), false);
});

services.AddSettings<InputOptions>("input", input =>
{
    var defaultJump = InputActionBindings.FromKeyCode(
        (long)Key.Space,
        keyCode => OS.GetKeycodeString((Key)keyCode));

    input.Map(x => x.Jump)
        .WithUi("Jump", ConfigUiControl.KeyBinding)
        .PersistAs("jump")
        .ToRuntime(
            new GodotInputActionBinding("jump", defaultJump),
            defaultJump,
            InputActionBindingConfigValueCodec.Instance);
});
```

Settings are persisted to `user://settings.cfg` by default. Project settings can be used as defaults, with player changes saved in the settings file.

Use small POCOs with mapped scalar properties. Use an explicit `IConfigValueCodec<TValue>` when a value spans multiple persisted fields, such as `InputActionBindings`.

The settings file uses Godot `ConfigFile` sections, slash paths, and native values:

```ini
[audio]

master/volume=0.8
master/muted=false

[input]

jump/0/type="key"
jump/0/key_code=74
jump/1/type="mouse_button"
jump/1/button_index=1

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

Place config menu controls in a Godot group matching
`settings-transaction:*` to make their settings monitor transactional. The
same control code can be used outside the menu for immediate updates and inside
the menu for draft updates:

```csharp
public partial class DifficultyControl : HBoxContainer
{
    private ISettingsMonitor<GameplayOptions>? _gameplay;

    public override void _Ready()
    {
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
    }
}
```

Resolve `ISettingsTransaction` from the menu node to save or abandon all draft
changes made by controls in the same transaction group.
