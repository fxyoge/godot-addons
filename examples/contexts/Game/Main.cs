using System;
using System.Linq;
using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class Main : Control
{
    public override void _Ready()
    {
        var scroll = new ScrollContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(scroll);

        var rows = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(rows);

        AddHeader(rows);

        AddProbe(rows, "Runtime player 1", "runtime", "level:forest", "player:1", "team:red");
        AddProbe(rows, "Runtime player 2", "runtime", "level:forest", "player:2", "team:blue");
        AddProbe(rows, "Preview player 1", "preview", "level:forest", "player:1", "team:red");
        AddProbe(rows, "Preview desert camera", "preview", "level:desert", "camera:main");
        AddProbe(rows, "Replay forest", "replay", "level:forest", "recording:run42");
    }

    private static void AddHeader(VBoxContainer rows)
    {
        rows.AddChild(new Label
        {
            Text = "Fxyoge.DependencyInjection contexts",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
    }

    private void AddProbe(VBoxContainer rows, string title, params string[] groups)
    {
        var probe = new Node
        {
            Name = title.Replace(' ', '_'),
        };
        AddChild(probe);

        foreach (var group in groups)
        {
            probe.AddToGroup(group);
        }

        var settings = probe.GetRequiredService<SettingsManager>();
        var level = probe.GetRequiredService<ILevelSession>();
        var player = probe.GetService<IPlayerProfile>();
        var mode = probe.GetRequiredService<IControlMode>();
        var debug = probe.GetService<IDebugOverlayModel>();
        var camera = probe.GetService<ICameraRig>();

        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        rows.AddChild(panel);

        var text = string.Join(
            System.Environment.NewLine,
            title,
            $"groups: {string.Join(", ", groups)}",
            $"settings manager: {settings.Id}",
            $"settings stores: {string.Join(", ", settings.Stores.Select(store => store.Label))}",
            $"level session: {level.Id}",
            $"player profile: {FormatPlayer(player)}",
            $"control mode: {mode.Label}",
            $"debug overlay: {debug?.Id ?? "none"}",
            $"camera rig: {camera?.Id ?? "none"}");

        panel.AddChild(new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private static string FormatPlayer(IPlayerProfile? player)
        => player is null
            ? "none"
            : $"{player.Id} via level {player.LevelSessionId}";
}
