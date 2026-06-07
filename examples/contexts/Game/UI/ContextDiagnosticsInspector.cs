using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class ContextDiagnosticsInspector : PanelContainer
{
    private readonly List<VehicleActor> _actors = new();
    private readonly HashSet<string> _selectedContextGroups = new(StringComparer.Ordinal);
    private RichTextLabel? _contextSummary;
    private GridContainer? _matrix;
    private RichTextLabel? _details;
    private GridContainer? _contextToggles;
    private RichTextLabel? _contextServices;
    private RichTextLabel? _serviceInventory;
    private ServiceResolutionDiagnosticsSnapshot? _diagnostics;
    private ImmutableArray<string> _availableContextGroups = ImmutableArray<string>.Empty;
    private string _selectedActorName = "Player";
    private Type _selectedServiceType = typeof(VehicleController);

    public override void _Ready()
    {
        _contextSummary = GetNode<RichTextLabel>("%ContextSummary");
        _matrix = GetNode<GridContainer>("%ServiceMatrix");
        _details = GetNode<RichTextLabel>("%Details");
        _contextToggles = GetNode<GridContainer>("%ContextToggles");
        _contextServices = GetNode<RichTextLabel>("%ContextServices");
        _serviceInventory = GetNode<RichTextLabel>("%ServiceInventory");
    }

    public void Capture(IEnumerable<VehicleActor> actors)
    {
        _actors.Clear();
        _actors.AddRange(actors);

        if (_actors.Count > 0)
        {
            _selectedActorName = _actors[0].DisplayName;
        }

        RebuildAvailableContextGroups();
        CaptureDiagnostics();
        Refresh();
    }

    public void Refresh()
    {
        if (_contextSummary is null
            || _matrix is null
            || _details is null
            || _contextToggles is null
            || _contextServices is null
            || _serviceInventory is null
            || _diagnostics is null)
        {
            return;
        }

        _contextSummary.Text = BuildContextSummaryText();
        RebuildServiceMatrix(_diagnostics);
        _details.Text = BuildDetailsText(_diagnostics);
        RebuildContextToggles();
        _contextServices.Text = BuildContextServicesText();
        _serviceInventory.Text = BuildServiceInventoryText(_diagnostics);
    }

    private void CaptureDiagnostics()
    {
        var gameServices = GetTree().Root.GetNode<GameServices>("GameServices");
        gameServices.ClearDiagnostics();

        foreach (var actor in _actors)
        {
            foreach (var probe in ContextServiceProbes.All)
            {
                _ = gameServices.GetRequiredService(actor, probe.Type);
            }
        }

        _diagnostics = gameServices.CreateDiagnosticsSnapshot();
    }

    private string BuildContextSummaryText()
    {
        var text = new StringBuilder();
        text.AppendLine("CONTEXT NODES");
        foreach (var actor in _actors)
        {
            text.AppendLine($"{actor.DisplayName,-7} {string.Join(", ", GetDisplayGroups(actor))}");
        }
        return text.ToString();
    }

    private void RebuildServiceMatrix(ServiceResolutionDiagnosticsSnapshot diagnostics)
    {
        if (_matrix is null)
        {
            return;
        }

        ClearChildren(_matrix);
        _matrix.Columns = _actors.Count + 1;

        _matrix.AddChild(CreateHeaderLabel("service"));
        foreach (var actor in _actors)
        {
            _matrix.AddChild(CreateHeaderLabel(actor.DisplayName));
        }

        foreach (var probe in ContextServiceProbes.All)
        {
            _matrix.AddChild(CreateHeaderLabel(probe.Label));
            foreach (var actor in _actors)
            {
                var trace = FindTrace(diagnostics, actor, probe.Type);
                _matrix.AddChild(CreateMatrixButton(actor, probe, trace));
            }
        }
    }

    private void RebuildContextToggles()
    {
        if (_contextToggles is null)
        {
            return;
        }

        ClearChildren(_contextToggles);
        _contextToggles.Columns = 3;

        foreach (var group in _availableContextGroups)
        {
            var button = new Button
            {
                Text = group,
                ToggleMode = true,
                ButtonPressed = _selectedContextGroups.Contains(group),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = group,
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            button.Pressed += () =>
            {
                if (button.ButtonPressed)
                {
                    _selectedContextGroups.Add(group);
                }
                else
                {
                    _selectedContextGroups.Remove(group);
                }

                Refresh();
            };
            _contextToggles.AddChild(button);
        }
    }

    private string BuildDetailsText(ServiceResolutionDiagnosticsSnapshot diagnostics)
    {
        if (_actors.Count == 0)
        {
            return "No actors found.";
        }

        var actor = _actors.FirstOrDefault(actor => actor.DisplayName == _selectedActorName) ?? _actors[0];
        var probe = ContextServiceProbes.All.FirstOrDefault(probe => probe.Type == _selectedServiceType);
        if (probe.Type is null)
        {
            probe = ContextServiceProbes.All[0];
        }

        var trace = FindTrace(diagnostics, actor, probe.Type);
        var text = new StringBuilder();

        text.AppendLine();
        text.AppendLine($"SELECTED: {actor.DisplayName} / {probe.Label}");
        text.AppendLine($"groups: {string.Join(", ", GetDisplayGroups(actor))}");
        text.AppendLine();

        if (trace is null)
        {
            text.AppendLine("missing trace");
            return text.ToString();
        }

        text.AppendLine("RESOLUTION GRAPH");
        AppendNode(text, trace.Root, 0);
        text.AppendLine();

        text.AppendLine("CONTEXTUAL RULE DECISIONS");
        foreach (var registration in trace.Root.ContextualRegistrations)
        {
            var state = registration.Matches ? "match" : "skip ";
            var partition = registration.Partition is null ? "" : $" partition={Shorten(registration.Partition, 56)}";
            text.AppendLine(
                $"  {state} {ShortType(registration.ImplementationType),-24} rule={registration.Rule}{partition}");
        }

        if (trace.Root.ContextualRegistrations.Length == 0)
        {
            text.AppendLine("  no contextual registrations for this service type");
        }

        return text.ToString();
    }

    private string BuildContextServicesText()
    {
        var matches = GetTree()
            .Root
            .GetNode<GameServices>("GameServices")
            .GetContextualServices(GetSelectedContextGroups());
        var text = new StringBuilder();

        if (matches.Length == 0)
        {
            return "No contextual services match.";
        }

        foreach (var match in matches)
        {
            text.AppendLine(
                $"{ShortType(match.ServiceType)} -> {ShortType(match.ImplementationType)} ({match.Lifetime}, rule={match.Rule})");
        }

        return text.ToString();
    }

    private static string BuildServiceInventoryText(ServiceResolutionDiagnosticsSnapshot diagnostics)
    {
        var text = new StringBuilder();
        foreach (var instance in diagnostics.Instances
            .OrderBy(instance => instance.Source)
            .ThenBy(instance => instance.Partition ?? "")
            .ThenBy(instance => instance.ImplementationType.Name))
        {
            var lifetime = instance.Lifetime?.ToString() ?? "n/a";
            var partition = instance.Partition is null ? "root/no partition" : Shorten(instance.Partition, 56);
            text.AppendLine(
                $"{instance.Id,-28} {instance.Source,-10} {lifetime,-9} {ShortType(instance.ServiceType)} -> {ShortType(instance.ImplementationType)}");
            text.AppendLine($"  {partition}");
        }

        return text.ToString();
    }

    private static Label CreateHeaderLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    private Button CreateMatrixButton(VehicleActor actor, ServiceProbe probe, ServiceResolutionTrace? trace)
    {
        var selected = actor.DisplayName == _selectedActorName && probe.Type == _selectedServiceType;
        var button = new Button
        {
            Text = Shorten(DescribeCell(trace), 18),
            ToggleMode = true,
            ButtonPressed = selected,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = trace is null ? "No trace" : DescribeCell(trace),
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += () =>
        {
            _selectedActorName = actor.DisplayName;
            _selectedServiceType = probe.Type;
            Refresh();
        };
        return button;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void AppendNode(StringBuilder text, ServiceResolutionTraceNode node, int depth)
    {
        var prefix = new string(' ', depth * 2);
        text.AppendLine(
            $"{prefix}{ShortType(node.ServiceType)} -> {DescribeNodeResult(node)}");

        foreach (var item in node.Items)
        {
            text.AppendLine(
                $"{prefix}  item {ShortType(item.ServiceType)} -> {ShortType(item.ImplementationType)} {item.InstanceId}");
        }

        foreach (var dependency in node.Dependencies)
        {
            AppendNode(text, dependency, depth + 1);
        }
    }

    private ServiceResolutionTrace? FindTrace(
        ServiceResolutionDiagnosticsSnapshot diagnostics,
        VehicleActor actor,
        Type serviceType)
    {
        var groups = GetResolutionGroups(actor).OrderBy(group => group).ToImmutableArray();
        return FindTrace(diagnostics, groups, serviceType);
    }

    private static ServiceResolutionTrace? FindTrace(
        ServiceResolutionDiagnosticsSnapshot diagnostics,
        ImmutableArray<string> groups,
        Type serviceType)
    {
        return diagnostics.Traces
            .LastOrDefault(trace => trace.ServiceType == serviceType
                && trace.ContextGroups.SequenceEqual(groups));
    }

    private void RebuildAvailableContextGroups()
    {
        _availableContextGroups = _actors
            .SelectMany(GetDisplayGroups)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        _selectedContextGroups.RemoveWhere(group => !_availableContextGroups.Contains(group));
    }

    private static string[] GetDisplayGroups(Node node)
        => node.GetGroups()
            .Select(group => group.ToString())
            .Where(group => group != VehicleActor.SceneGroup && !group.StartsWith("_"))
            .OrderBy(group => group)
            .ToArray();

    private static string[] GetResolutionGroups(Node node)
        => node.GetGroups()
            .Select(group => group.ToString())
            .Where(group => !group.StartsWith("_"))
            .OrderBy(group => group)
            .ToArray();

    private ImmutableArray<string> GetSelectedContextGroups()
        => _selectedContextGroups
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static string DescribeCell(ServiceResolutionTrace? trace)
        => trace is null ? "no trace" : DescribeNodeResult(trace.Root);

    private static string DescribeNodeResult(ServiceResolutionTraceNode node)
    {
        if (node.Source == "enumerable")
        {
            return string.Join(" + ", node.Items.Select(item => ShortType(item.ImplementationType)));
        }

        var cache = node.CacheHit is null ? "" : node.CacheHit.Value ? " hit" : " new";
        return $"{ShortType(node.ImplementationType)} {node.InstanceId}{cache}";
    }

    private static string ShortType(Type? type)
    {
        if (type is null)
        {
            return "null";
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return $"IEnumerable<{ShortType(type.GetGenericArguments()[0])}>";
        }

        if (type.IsArray)
        {
            return $"{ShortType(type.GetElementType())}[]";
        }

        return type.Name;
    }

    private static string Shorten(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
}
