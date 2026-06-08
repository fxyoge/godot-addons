using System;
using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class ConfigMenu : PanelContainer
{
    private ISettingsTransaction? _transaction;
    private ISettingsMonitor<AudioOptions>? _audio;
    private ISettingsMonitor<InputOptions>? _input;
    private ISettingsMonitor<GameplayOptions>? _gameplay;
    private ISettingsMonitor<ProjectDefaultsOptions>? _project;
    private Button? _closeButton;
    private Button? _cancelButton;
    private Button? _saveButton;
    private Button? _resetButton;
    private Button? _filesButton;

    public event Action? Closed;

    public override void _Ready()
    {
        _transaction = this.GetRequiredService<ISettingsTransaction>();
        _audio = this.GetRequiredService<ISettingsMonitor<AudioOptions>>();
        _input = this.GetRequiredService<ISettingsMonitor<InputOptions>>();
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();
        _project = this.GetRequiredService<ISettingsMonitor<ProjectDefaultsOptions>>();

        _closeButton = GetNode<Button>("PanelMargin/PanelRoot/Header/CloseButton");
        _cancelButton = GetNode<Button>("PanelMargin/PanelRoot/Footer/CancelButton");
        _saveButton = GetNode<Button>("PanelMargin/PanelRoot/Footer/SaveButton");
        _resetButton = GetNode<Button>("PanelMargin/PanelRoot/Footer/ResetButton");
        _filesButton = GetNode<Button>("PanelMargin/PanelRoot/Footer/FilesButton");

        _closeButton.Pressed += Cancel;
        _cancelButton.Pressed += Cancel;
        _saveButton.Pressed += async () =>
        {
            await _transaction.Save();
            Closed?.Invoke();
        };
        _resetButton.Pressed += ResetAll;
        _filesButton.Pressed += OpenSettingsFolder;
    }

    private async void ResetAll()
    {
        await _audio!.Reset();
        await _input!.Reset();
        await _gameplay!.Reset();
        await _project!.Reset();
    }

    private void Cancel()
    {
        _transaction!.Abandon();
        Closed?.Invoke();
    }

    private static void OpenSettingsFolder()
    {
        var settingsDirectory = ProjectSettings.GlobalizePath("user://");
        ConfigMenuErrors.ThrowIfDirectoryCreationFailed(
            settingsDirectory,
            DirAccess.MakeDirRecursiveAbsolute(settingsDirectory));

        ConfigMenuErrors.ThrowIfShellOpenFailed(
            settingsDirectory,
            OS.ShellOpen(settingsDirectory));
    }
}
