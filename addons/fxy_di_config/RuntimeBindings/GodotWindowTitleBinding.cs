using System;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotWindowTitleBinding : IRuntimeConfigBinding<string>
{
    private readonly string _projectSettingPath;
    private string? _currentTitle;

    public GodotWindowTitleBinding(string projectSettingPath = "application/config/name")
    {
        _projectSettingPath = projectSettingPath;
    }

    public string CaptureDefault()
    {
        _currentTitle = ReadProjectTitle();
        return _currentTitle;
    }

    public string ReadCurrent() => _currentTitle ?? ReadProjectTitle();

    public void Apply(string value)
    {
        DisplayServer.WindowSetTitle(value);
        _currentTitle = value;
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(string),
            ConfigValueSource.ProjectSettings,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.Text));

    private string ReadProjectTitle()
    {
        if (!ProjectSettings.HasSetting(_projectSettingPath))
        {
            throw new InvalidOperationException(
                $"ProjectSettings '{_projectSettingPath}' does not exist and cannot be used as the window title default.");
        }

        var value = ProjectSettings.GetSetting(_projectSettingPath).Obj;
        if (value is string title)
        {
            return title;
        }

        throw new InvalidOperationException(
            $"ProjectSettings '{_projectSettingPath}' is '{value?.GetType().FullName ?? "null"}', not '{typeof(string).FullName}'.");
    }
}
