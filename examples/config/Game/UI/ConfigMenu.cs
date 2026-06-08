using System;
using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class ConfigMenu : PanelContainer
{
    private ISettingsTransaction? _transaction;
    private ISettingsMonitor<GameplayOptions>? _gameplay;
    private Button? _closeButton;
    private Button? _cancelButton;
    private Button? _saveButton;
    private Button? _resetButton;
    private Button? _filesButton;

    public event Action? Closed;

    public override void _Ready()
    {
        _transaction = this.GetRequiredService<ISettingsTransaction>();
        _gameplay = this.GetRequiredService<ISettingsMonitor<GameplayOptions>>();

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
        _resetButton.Pressed += async () => await _gameplay.Reset();
        _filesButton.Pressed += OpenSettingsFolder;
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

        var error = OS.ShellOpen(settingsDirectory);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not open settings folder '{settingsDirectory}': {error}");
        }
    }
}
