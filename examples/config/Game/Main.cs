using System;
using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class Main : Control
{
    private IWritableOptionsMonitor<AudioOptions>? _audio;
    private IWritableOptionsMonitor<InputOptions>? _input;
    private IWritableOptionsMonitor<GameplayOptions>? _gameplay;
    private IWritableOptionsMonitor<ProjectDefaultsOptions>? _project;
    private Label? _summary;
    private Button? _jumpButton;
    private HSlider? _volumeSlider;
    private CheckBox? _muteToggle;
    private OptionButton? _difficultySelect;
    private CheckBox? _damageNumbersToggle;
    private HSlider? _sensitivitySlider;
    private LineEdit? _titleEdit;

    public override void _Ready()
    {
        var services = GetTree()
            .Root
            .GetNode<GameServices>("GameServices");

        _audio = services.GetRequiredService<IWritableOptionsMonitor<AudioOptions>>();
        _input = services.GetRequiredService<IWritableOptionsMonitor<InputOptions>>();
        _gameplay = services.GetRequiredService<IWritableOptionsMonitor<GameplayOptions>>();
        _project = services.GetRequiredService<IWritableOptionsMonitor<ProjectDefaultsOptions>>();

        BuildUi();
        ConnectMonitors();
        RefreshUi();
    }

    private void BuildUi()
    {
        var margin = new MarginContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label
        {
            Text = "Fxy DI Config",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        root.AddChild(title);

        var summaryRow = new HBoxContainer();
        summaryRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(summaryRow);

        _summary = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        summaryRow.AddChild(_summary);

        var openSettingsFolder = new Button
        {
            Text = "Open Settings Folder",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        openSettingsFolder.Pressed += OpenSettingsFolder;
        summaryRow.AddChild(openSettingsFolder);

        root.AddChild(MakeSeparator());
        AddAudioControls(root);
        root.AddChild(MakeSeparator());
        AddInputControls(root);
        root.AddChild(MakeSeparator());
        AddGameplayControls(root);
        root.AddChild(MakeSeparator());
        AddProjectControls(root);

        var resetAll = new Button { Text = "Reset All Sections" };
        resetAll.Pressed += async () =>
        {
            await _audio!.Reset();
            await _input!.Reset();
            await _gameplay!.Reset();
            await _project!.Reset();
        };
        root.AddChild(resetAll);
    }

    private void AddAudioControls(VBoxContainer root)
    {
        root.AddChild(MakeSectionLabel("Godot AudioServer"));

        _volumeSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            CustomMinimumSize = new Vector2(320, 32),
        };
        _volumeSlider.ValueChanged += async value =>
        {
            await _audio!.Update(options => options.MasterVolume = (float)value);
            RefreshUi();
        };
        root.AddChild(MakeRow("Master volume", _volumeSlider));

        _muteToggle = new CheckBox { Text = "Muted" };
        _muteToggle.Toggled += async value =>
        {
            await _audio!.Update(options => options.Muted = value);
            RefreshUi();
        };
        root.AddChild(_muteToggle);
    }

    private void AddInputControls(VBoxContainer root)
    {
        root.AddChild(MakeSectionLabel("Godot InputMap"));

        _jumpButton = new Button();
        _jumpButton.Pressed += async () =>
        {
            var current = _input!.CurrentValue.Jump.KeyCode;
            var next = current == (long)Key.J ? Key.Space : Key.J;
            await _input.Update(options =>
            {
                options.Jump = new InputActionBinding
                {
                    KeyCode = (long)next,
                    DisplayName = OS.GetKeycodeString(next),
                };
            });
            RefreshUi();
        };
        root.AddChild(MakeRow("Jump binding", _jumpButton));
    }

    private void AddGameplayControls(VBoxContainer root)
    {
        root.AddChild(MakeSectionLabel("Custom gameplay config"));

        _difficultySelect = new OptionButton();
        foreach (var difficulty in new[] { "Easy", "Normal", "Hard" })
        {
            _difficultySelect.AddItem(difficulty);
        }

        _difficultySelect.ItemSelected += async index =>
        {
            await _gameplay!.Update(options => options.Difficulty = _difficultySelect.GetItemText((int)index));
            RefreshUi();
        };
        root.AddChild(MakeRow("Difficulty", _difficultySelect));

        _damageNumbersToggle = new CheckBox { Text = "Show damage numbers" };
        _damageNumbersToggle.Toggled += async value =>
        {
            await _gameplay!.Update(options => options.ShowDamageNumbers = value);
            RefreshUi();
        };
        root.AddChild(_damageNumbersToggle);

        _sensitivitySlider = new HSlider
        {
            MinValue = 0.1,
            MaxValue = 2.0,
            Step = 0.05,
            CustomMinimumSize = new Vector2(320, 32),
        };
        _sensitivitySlider.ValueChanged += async value =>
        {
            await _gameplay!.Update(options => options.CameraSensitivity = (float)value);
            RefreshUi();
        };
        root.AddChild(MakeRow("Camera sensitivity", _sensitivitySlider));
    }

    private void AddProjectControls(VBoxContainer root)
    {
        root.AddChild(MakeSectionLabel("ProjectSettings default + user override"));

        _titleEdit = new LineEdit();
        _titleEdit.TextSubmitted += async value =>
        {
            await _project!.Update(options => options.GameTitle = value);
            RefreshUi();
        };
        root.AddChild(MakeRow("Game title", _titleEdit));

        var applyTitle = new Button { Text = "Apply Title Text" };
        applyTitle.Pressed += async () =>
        {
            await _project!.Update(options => options.GameTitle = _titleEdit!.Text);
            RefreshUi();
        };
        root.AddChild(applyTitle);
    }

    private void ConnectMonitors()
    {
        _audio!.OnChange(_ => CallDeferred(MethodName.RefreshUi));
        _input!.OnChange(_ => CallDeferred(MethodName.RefreshUi));
        _gameplay!.OnChange(_ => CallDeferred(MethodName.RefreshUi));
        _project!.OnChange(_ => CallDeferred(MethodName.RefreshUi));
    }

    private void RefreshUi()
    {
        if (_summary is null)
        {
            return;
        }

        var audio = _audio!.CurrentValue;
        var input = _input!.CurrentValue;
        var gameplay = _gameplay!.CurrentValue;
        var project = _project!.CurrentValue;

        _volumeSlider!.SetValueNoSignal(audio.MasterVolume);
        _muteToggle!.SetPressedNoSignal(audio.Muted);
        _jumpButton!.Text = $"Toggle Space/J - current: {input.Jump.DisplayName}";
        _difficultySelect!.Select(Math.Max(0, GetDifficultyIndex(gameplay.Difficulty)));
        _damageNumbersToggle!.SetPressedNoSignal(gameplay.ShowDamageNumbers);
        _sensitivitySlider!.SetValueNoSignal(gameplay.CameraSensitivity);
        _titleEdit!.Text = project.GameTitle;

        _summary.Text =
            $"Effective settings are live and persisted to user://settings.cfg.\n" +
            $"Title: {project.GameTitle}; difficulty: {gameplay.Difficulty}; " +
            $"volume: {audio.MasterVolume:0.00}; muted: {audio.Muted}; jump: {input.Jump.DisplayName}.";
    }

    private static int GetDifficultyIndex(string difficulty)
        => difficulty switch
        {
            "Easy" => 0,
            "Normal" => 1,
            "Hard" => 2,
            _ => 1,
        };

    private static void OpenSettingsFolder()
    {
        var settingsDirectory = ProjectSettings.GlobalizePath("user://");
        DirAccess.MakeDirRecursiveAbsolute(settingsDirectory);

        var error = OS.ShellOpen(settingsDirectory);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not open settings folder '{settingsDirectory}': {error}");
        }
    }

    private static Label MakeSectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private static HSeparator MakeSeparator() => new();

    private static Control MakeRow(string labelText, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(180, 0),
        });
        row.AddChild(control);
        return row;
    }
}
