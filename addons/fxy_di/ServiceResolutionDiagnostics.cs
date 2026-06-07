using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public sealed record ServiceResolutionDiagnosticsSnapshot(
    ImmutableArray<ServiceResolutionTrace> Traces,
    ImmutableArray<ServiceInstanceDiagnostic> Instances);

public sealed record ServiceResolutionTrace(
    int Id,
    Type ServiceType,
    ImmutableArray<string> ContextGroups,
    ServiceResolutionTraceNode Root);

public sealed record ServiceResolutionTraceNode(
    int Id,
    Type ServiceType,
    string Source,
    Type? ImplementationType,
    string? InstanceId,
    ServiceLifetime? Lifetime,
    string? Rule,
    string? Partition,
    bool? CacheHit,
    ImmutableArray<ContextualRegistrationDiagnostic> ContextualRegistrations,
    ImmutableArray<ServiceResolutionItem> Items,
    ImmutableArray<ServiceResolutionTraceNode> Dependencies);

public sealed record ServiceResolutionItem(
    Type ServiceType,
    string Source,
    Type? ImplementationType,
    string? InstanceId,
    ServiceLifetime? Lifetime,
    string? Rule,
    string? Partition,
    bool? CacheHit);

public sealed record ContextualRegistrationDiagnostic(
    Type ServiceType,
    Type ImplementationType,
    ServiceLifetime Lifetime,
    string Rule,
    bool Matches,
    string? Partition);

public sealed record ServiceInstanceDiagnostic(
    string Id,
    Type ServiceType,
    Type ImplementationType,
    string Source,
    ServiceLifetime? Lifetime,
    string? Rule,
    string? Partition);

internal sealed class ServiceResolutionDiagnostics
{
    private readonly object _lock = new();
    private readonly List<ServiceResolutionTrace> _traces = new();
    private readonly Dictionary<object, ServiceInstanceDiagnostic> _instances = new(ReferenceEqualityComparer.Instance);
    private int _nextTraceId;
    private int _nextNodeId;
    private int _nextInstanceId;

    [ThreadStatic]
    private static TraceBuilder? currentTrace;

    public bool IsTracing => currentTrace is not null;

    public TraceScope BeginTrace(Type serviceType, ContextualResolutionContext context)
    {
        if (currentTrace is not null)
        {
            return new TraceScope(this, null);
        }

        var trace = new TraceBuilder(NextTraceId(), serviceType, context.Groups);
        currentTrace = trace;
        return new TraceScope(this, trace);
    }

    public NodeScope BeginNode(Type serviceType)
    {
        var trace = currentTrace;
        if (trace is null)
        {
            return new NodeScope(null);
        }

        var node = new TraceNodeBuilder(NextNodeId(), serviceType);
        trace.Push(node);
        return new NodeScope(node);
    }

    public string TrackInstance(
        object instance,
        Type serviceType,
        Type implementationType,
        string source,
        ServiceLifetime? lifetime,
        string? rule,
        string? partition)
    {
        lock (_lock)
        {
            if (_instances.TryGetValue(instance, out var existing))
            {
                return existing.Id;
            }

            var id = $"{implementationType.Name}:{++_nextInstanceId:000}";
            _instances.Add(
                instance,
                new ServiceInstanceDiagnostic(id, serviceType, implementationType, source, lifetime, rule, partition));
            return id;
        }
    }

    public ServiceResolutionDiagnosticsSnapshot CreateSnapshot()
    {
        lock (_lock)
        {
            return new ServiceResolutionDiagnosticsSnapshot(
                _traces.ToImmutableArray(),
                _instances.Values.ToImmutableArray());
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _traces.Clear();
            _instances.Clear();
            _nextTraceId = 0;
            _nextNodeId = 0;
            _nextInstanceId = 0;
        }
    }

    private int NextTraceId()
    {
        lock (_lock)
        {
            return ++_nextTraceId;
        }
    }

    private int NextNodeId()
    {
        lock (_lock)
        {
            return ++_nextNodeId;
        }
    }

    private void CompleteTrace(TraceBuilder trace)
    {
        var completed = trace.Build();
        if (completed is null)
        {
            return;
        }

        lock (_lock)
        {
            _traces.Add(completed);
        }
    }

    public readonly struct TraceScope : IDisposable
    {
        private readonly ServiceResolutionDiagnostics? diagnostics;
        private readonly TraceBuilder? trace;

        public TraceScope(ServiceResolutionDiagnostics diagnostics, TraceBuilder? trace)
        {
            this.diagnostics = diagnostics;
            this.trace = trace;
        }

        public void Dispose()
        {
            if (diagnostics is null || trace is null)
            {
                return;
            }

            currentTrace = null;
            diagnostics.CompleteTrace(trace);
        }
    }

    public readonly struct NodeScope : IDisposable
    {
        private readonly TraceNodeBuilder? node;

        public NodeScope(TraceNodeBuilder? node)
        {
            this.node = node;
        }

        public TraceNodeBuilder? Node => node;

        public void Dispose()
        {
            currentTrace?.Pop(node);
        }
    }

    internal sealed class TraceBuilder
    {
        private readonly Stack<TraceNodeBuilder> stack = new();
        private readonly ImmutableArray<string> contextGroups;
        private TraceNodeBuilder? root;

        public TraceBuilder(int id, Type serviceType, IEnumerable<string> groups)
        {
            Id = id;
            ServiceType = serviceType;
            contextGroups = groups.Order(StringComparer.Ordinal).ToImmutableArray();
        }

        public int Id { get; }

        public Type ServiceType { get; }

        public void Push(TraceNodeBuilder node)
        {
            if (stack.TryPeek(out var parent))
            {
                parent.Dependencies.Add(node);
            }
            else
            {
                root = node;
            }

            stack.Push(node);
        }

        public void Pop(TraceNodeBuilder? node)
        {
            if (node is null || stack.Count == 0)
            {
                return;
            }

            var popped = stack.Pop();
            if (!ReferenceEquals(popped, node))
            {
                throw new InvalidOperationException("Service resolution diagnostics stack became unbalanced.");
            }
        }

        public ServiceResolutionTrace? Build()
            => root is null
                ? null
                : new ServiceResolutionTrace(Id, ServiceType, contextGroups, root.Build());
    }

    internal sealed class TraceNodeBuilder
    {
        public TraceNodeBuilder(int id, Type serviceType)
        {
            Id = id;
            ServiceType = serviceType;
        }

        public int Id { get; }

        public Type ServiceType { get; }

        public string Source { get; set; } = "unresolved";

        public Type? ImplementationType { get; set; }

        public string? InstanceId { get; set; }

        public ServiceLifetime? Lifetime { get; set; }

        public string? Rule { get; set; }

        public string? Partition { get; set; }

        public bool? CacheHit { get; set; }

        public List<ContextualRegistrationDiagnostic> ContextualRegistrations { get; } = new();

        public List<ServiceResolutionItem> Items { get; } = new();

        public List<TraceNodeBuilder> Dependencies { get; } = new();

        public ServiceResolutionTraceNode Build()
            => new(
                Id,
                ServiceType,
                Source,
                ImplementationType,
                InstanceId,
                Lifetime,
                Rule,
                Partition,
                CacheHit,
                ContextualRegistrations.ToImmutableArray(),
                Items.ToImmutableArray(),
                Dependencies.Select(dependency => dependency.Build()).ToImmutableArray());
    }
}
