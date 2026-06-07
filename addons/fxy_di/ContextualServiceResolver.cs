using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

internal sealed class ContextualServiceResolver : IDisposable
{
    private readonly ServiceProvider _rootProvider;
    private readonly ImmutableArray<ServiceDescriptor> _serviceDescriptors;
    private readonly ImmutableArray<ContextualServiceRegistration> _contextualRegistrations;
    private readonly Dictionary<ContextualCacheKey, object> _cache = new();
    private readonly List<IDisposable> _transientDisposables = new();
    private readonly ServiceResolutionDiagnostics _diagnostics = new();
    private readonly object _lock = new();

    public ContextualServiceResolver(ServiceProvider rootProvider, IEnumerable<ServiceDescriptor> serviceDescriptors)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        ArgumentNullException.ThrowIfNull(serviceDescriptors);

        _rootProvider = rootProvider;
        _serviceDescriptors = serviceDescriptors.ToImmutableArray();
        _contextualRegistrations = _serviceDescriptors
            .Where(descriptor => descriptor.ServiceType == typeof(ContextualServiceRegistration)
                && descriptor.ImplementationInstance is ContextualServiceRegistration)
            .Select(descriptor => (ContextualServiceRegistration)descriptor.ImplementationInstance!)
            .ToImmutableArray();
    }

    public object? GetService(Type serviceType, ContextualResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(context);

        using var trace = _diagnostics.BeginTrace(serviceType, context);
        var provider = new ContextualServiceProvider(this, context);
        return provider.GetService(serviceType);
    }

    public ServiceResolutionDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        => _diagnostics.CreateSnapshot();

    public ImmutableArray<ContextualServiceMatch> GetContextualServices(ContextualResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _contextualRegistrations
            .Where(registration => registration.Rule.Match(context.Groups) is not null)
            .Select(registration => new ContextualServiceMatch(
                registration.ServiceType,
                registration.ImplementationType,
                registration.Lifetime,
                registration.Rule.ToString()))
            .OrderBy(match => match.ServiceType.Name, StringComparer.Ordinal)
            .ThenBy(match => match.ImplementationType.Name, StringComparer.Ordinal)
            .ThenBy(match => match.Rule, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public void ClearDiagnostics() => _diagnostics.Clear();

    public void DisposeContext(ContextualResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Conservative first version: inferred shared partitions are intentionally
        // retained until the resolver is disposed.
    }

    public void Dispose()
    {
        List<IDisposable> disposables;

        lock (_lock)
        {
            disposables = _cache.Values
                .OfType<IDisposable>()
                .Reverse()
                .Concat(_transientDisposables.AsEnumerable().Reverse())
                .ToList();
            _cache.Clear();
            _transientDisposables.Clear();
        }

        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    private object? Resolve(Type serviceType, ContextualResolutionContext context)
    {
        if (IsDiagnosticsNoise(serviceType))
        {
            return ResolveCore(serviceType, context, null);
        }

        using var nodeScope = _diagnostics.BeginNode(serviceType);
        return ResolveCore(serviceType, context, nodeScope.Node);
    }

    private object? ResolveCore(
        Type serviceType,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        if (TryGetEnumerableElementType(serviceType, out var elementType))
        {
            var enumerable = ResolveEnumerable(elementType, context, node);
            if (node is not null)
            {
                node.Source = "enumerable";
                node.ImplementationType = enumerable.GetType();
                node.InstanceId = _diagnostics.TrackInstance(
                    enumerable,
                    serviceType,
                    enumerable.GetType(),
                    "enumerable",
                    null,
                    null,
                    null);
            }

            return enumerable;
        }

        var contextualMatch = FindBestContextualRegistration(serviceType, context, node);
        if (contextualMatch is not null)
        {
            return ResolveContextual(contextualMatch.Value, context, node);
        }

        var normalDescriptor = _serviceDescriptors
            .Where(descriptor => descriptor.ServiceType == serviceType)
            .Where(IsNormalServiceDescriptor)
            .LastOrDefault();

        if (normalDescriptor is null)
        {
            if (node is not null)
            {
                node.Source = "missing";
            }

            return null;
        }

        return ResolveNormal(normalDescriptor, context, node);
    }

    private Array ResolveEnumerable(
        Type elementType,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        var services = new List<object?>();

        foreach (var descriptor in _serviceDescriptors
            .Where(descriptor => descriptor.ServiceType == elementType)
            .Where(IsNormalServiceDescriptor))
        {
            var resolved = ResolveNormal(descriptor, context, null);
            services.Add(resolved);
            AddResolutionItem(node, elementType, "normal", descriptor, resolved, null, null);
        }

        foreach (var match in FindAllContextualRegistrations(elementType, context, node))
        {
            var resolved = ResolveContextual(match, context, null);
            services.Add(resolved);
            AddResolutionItem(
                node,
                elementType,
                "contextual",
                match.Registration,
                resolved,
                match.Registration.Lifetime == ServiceLifetime.Singleton
                    ? match.Registration.Rule.ToString()
                    : match.RuleMatch.InferredPartitionKey,
                null);
        }

        var array = Array.CreateInstance(elementType, services.Count);
        for (var index = 0; index < services.Count; index++)
        {
            array.SetValue(services[index], index);
        }

        return array;
    }

    private object ResolveNormal(
        ServiceDescriptor descriptor,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            UpdateNormalNode(node, descriptor, descriptor.ImplementationInstance, cacheHit: true);
            return descriptor.ImplementationInstance;
        }

        if (descriptor.Lifetime != ServiceLifetime.Transient)
        {
            var resolved = ResolveRootDescriptor(descriptor);
            UpdateNormalNode(node, descriptor, resolved, cacheHit: true);
            return resolved;
        }

        var provider = new ContextualServiceProvider(this, context);
        var created = TrackDisposable(CreateFromDescriptor(descriptor, provider));
        UpdateNormalNode(node, descriptor, created, cacheHit: false);
        return created;
    }

    private object ResolveRootDescriptor(ServiceDescriptor descriptor)
    {
        var services = (IEnumerable)_rootProvider.GetRequiredService(
            typeof(IEnumerable<>).MakeGenericType(descriptor.ServiceType));
        var normalDescriptors = _serviceDescriptors
            .Where(candidate => candidate.ServiceType == descriptor.ServiceType)
            .Where(IsNormalServiceDescriptor)
            .ToArray();
        var index = Array.IndexOf(normalDescriptors, descriptor);

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Could not locate service descriptor for '{descriptor.ServiceType}'.");
        }

        var currentIndex = 0;
        foreach (var service in services)
        {
            if (currentIndex == index)
            {
                return service
                    ?? throw new InvalidOperationException(
                        $"Root provider returned null for '{descriptor.ServiceType}'.");
            }

            currentIndex++;
        }

        throw new InvalidOperationException(
            $"Root provider did not return descriptor {index} for '{descriptor.ServiceType}'.");
    }

    private object ResolveContextual(
        ContextualRegistrationMatch match,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        if (match.Registration.Lifetime == ServiceLifetime.Transient)
        {
            var created = TrackDisposable(CreateContextual(match.Registration, context));
            UpdateContextualNode(node, match, created, null, cacheHit: false);
            return created;
        }

        var partition = match.Registration.Lifetime == ServiceLifetime.Singleton
            ? match.Registration.Rule.ToString()
            : match.RuleMatch.InferredPartitionKey;
        var cacheKey = new ContextualCacheKey(match.Registration, partition);

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                UpdateContextualNode(node, match, existing, partition, cacheHit: true);
                return existing;
            }

            var created = CreateContextual(match.Registration, context);
            _cache.Add(cacheKey, created);
            UpdateContextualNode(node, match, created, partition, cacheHit: false);
            return created;
        }
    }

    private object CreateContextual(
        ContextualServiceRegistration registration,
        ContextualResolutionContext context)
    {
        var provider = new ContextualServiceProvider(this, context);
        return ActivatorUtilities.CreateInstance(provider, registration.ImplementationType);
    }

    private object CreateFromDescriptor(ServiceDescriptor descriptor, IServiceProvider provider)
    {
        if (descriptor.ImplementationFactory is not null)
        {
            var service = descriptor.ImplementationFactory(provider);
            return service
                ?? throw new InvalidOperationException(
                    $"Factory for '{descriptor.ServiceType}' returned null.");
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Service descriptor for '{descriptor.ServiceType}' cannot be constructed contextually.");
    }

    private object TrackDisposable(object service)
    {
        if (service is IDisposable disposable)
        {
            lock (_lock)
            {
                _transientDisposables.Add(disposable);
            }
        }

        return service;
    }

    private ContextualRegistrationMatch? FindBestContextualRegistration(
        Type serviceType,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        var matches = FindAllContextualRegistrations(serviceType, context, node).ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        var best = matches
            .OrderByDescending(match => match.Registration.Rule.Specificity)
            .First();
        var tied = matches
            .Where(match => match.Registration.Rule.Specificity.CompareTo(best.Registration.Rule.Specificity) == 0)
            .ToArray();

        if (tied.Length > 1)
        {
            throw new InvalidOperationException(
                $"Contextual service '{serviceType}' has ambiguous matching registrations: "
                + string.Join("; ", tied.Select(match => match.Registration.Rule.ToString())));
        }

        return best;
    }

    private IEnumerable<ContextualRegistrationMatch> FindAllContextualRegistrations(
        Type serviceType,
        ContextualResolutionContext context,
        ServiceResolutionDiagnostics.TraceNodeBuilder? node)
    {
        foreach (var registration in _contextualRegistrations.Where(registration => registration.ServiceType == serviceType))
        {
            var match = registration.Rule.Match(context.Groups);
            if (node is not null)
            {
                node.ContextualRegistrations.Add(new ContextualRegistrationDiagnostic(
                    registration.ServiceType,
                    registration.ImplementationType,
                    registration.Lifetime,
                    registration.Rule.ToString(),
                    match is not null,
                    match?.InferredPartitionKey));
            }

            if (match is not null)
            {
                yield return new ContextualRegistrationMatch(registration, match.Value);
            }
        }
    }

    private void UpdateNormalNode(
        ServiceResolutionDiagnostics.TraceNodeBuilder? node,
        ServiceDescriptor descriptor,
        object resolved,
        bool cacheHit)
    {
        if (node is null)
        {
            TrackNormalInstance(descriptor, resolved);
            return;
        }

        node.Source = "normal";
        node.ImplementationType = resolved.GetType();
        node.InstanceId = TrackNormalInstance(descriptor, resolved);
        node.Lifetime = descriptor.Lifetime;
        node.CacheHit = cacheHit;
    }

    private void UpdateContextualNode(
        ServiceResolutionDiagnostics.TraceNodeBuilder? node,
        ContextualRegistrationMatch match,
        object resolved,
        string? partition,
        bool cacheHit)
    {
        var instanceId = TrackContextualInstance(match, resolved, partition);
        if (node is null)
        {
            return;
        }

        node.Source = "contextual";
        node.ImplementationType = match.Registration.ImplementationType;
        node.InstanceId = instanceId;
        node.Lifetime = match.Registration.Lifetime;
        node.Rule = match.Registration.Rule.ToString();
        node.Partition = partition;
        node.CacheHit = cacheHit;
    }

    private string TrackNormalInstance(ServiceDescriptor descriptor, object resolved)
        => _diagnostics.TrackInstance(
            resolved,
            descriptor.ServiceType,
            resolved.GetType(),
            "normal",
            descriptor.Lifetime,
            null,
            null);

    private string TrackContextualInstance(
        ContextualRegistrationMatch match,
        object resolved,
        string? partition)
        => _diagnostics.TrackInstance(
            resolved,
            match.Registration.ServiceType,
            match.Registration.ImplementationType,
            "contextual",
            match.Registration.Lifetime,
            match.Registration.Rule.ToString(),
            partition);

    private void AddResolutionItem(
        ServiceResolutionDiagnostics.TraceNodeBuilder? node,
        Type serviceType,
        string source,
        ServiceDescriptor descriptor,
        object? resolved,
        string? partition,
        bool? cacheHit)
    {
        if (node is null || resolved is null)
        {
            return;
        }

        node.Items.Add(new ServiceResolutionItem(
            serviceType,
            source,
            resolved.GetType(),
            TrackNormalInstance(descriptor, resolved),
            descriptor.Lifetime,
            null,
            partition,
            cacheHit));
    }

    private void AddResolutionItem(
        ServiceResolutionDiagnostics.TraceNodeBuilder? node,
        Type serviceType,
        string source,
        ContextualServiceRegistration registration,
        object? resolved,
        string? partition,
        bool? cacheHit)
    {
        if (node is null || resolved is null)
        {
            return;
        }

        node.Items.Add(new ServiceResolutionItem(
            serviceType,
            source,
            registration.ImplementationType,
            _diagnostics.TrackInstance(
                resolved,
                registration.ServiceType,
                registration.ImplementationType,
                "contextual",
                registration.Lifetime,
                registration.Rule.ToString(),
                partition),
            registration.Lifetime,
            registration.Rule.ToString(),
            partition,
            cacheHit));
    }

    private static bool IsNormalServiceDescriptor(ServiceDescriptor descriptor)
        => descriptor.ServiceType != typeof(ContextualServiceRegistration);

    private static bool IsDiagnosticsNoise(Type serviceType)
        => serviceType.FullName == "Microsoft.Extensions.DependencyInjection.IServiceProviderIsService";

    private static bool TryGetEnumerableElementType(Type serviceType, out Type elementType)
    {
        if (serviceType.IsGenericType
            && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = serviceType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private readonly record struct ContextualRegistrationMatch(
        ContextualServiceRegistration Registration,
        ContextualServiceRuleMatch RuleMatch);

    private readonly record struct ContextualCacheKey(
        ContextualServiceRegistration Registration,
        string Partition);

    private sealed class ContextualServiceProvider : IServiceProvider
    {
        private readonly ContextualServiceResolver _resolver;
        private readonly ContextualResolutionContext _context;

        public ContextualServiceProvider(
            ContextualServiceResolver resolver,
            ContextualResolutionContext context)
        {
            _resolver = resolver;
            _context = context;
        }

        public object? GetService(Type serviceType) => _resolver.Resolve(serviceType, _context);
    }
}
