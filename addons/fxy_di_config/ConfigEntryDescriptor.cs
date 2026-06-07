using System;

namespace Fxyoge.DependencyInjection.Configuration;

public enum ConfigValueSource
{
    UserConfig,
    InputMap,
    AudioBus,
    ProjectSettings,
    Runtime,
}

public enum ConfigUiControl
{
    Automatic,
    Toggle,
    Slider,
    Text,
    Select,
    KeyBinding,
}

public sealed record ConfigUiHint(
    string Label,
    ConfigUiControl Control = ConfigUiControl.Automatic,
    double? Min = null,
    double? Max = null,
    double? Step = null);

public sealed record ConfigEntryDescriptor(
    string Section,
    string Key,
    Type ValueType,
    ConfigValueSource Source,
    bool Writable,
    bool RuntimeMutable,
    bool RequiresRestart,
    ConfigUiHint? UiHint);
