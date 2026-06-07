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

        var provider = new ContextualServiceProvider(this, context);
        return provider.GetService(serviceType);
    }

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
        if (TryGetEnumerableElementType(serviceType, out var elementType))
        {
            return ResolveEnumerable(elementType, context);
        }

        var contextualMatch = FindBestContextualRegistration(serviceType, context);
        if (contextualMatch is not null)
        {
            return ResolveContextual(contextualMatch.Value, context);
        }

        var normalDescriptor = _serviceDescriptors
            .Where(descriptor => descriptor.ServiceType == serviceType)
            .Where(IsNormalServiceDescriptor)
            .LastOrDefault();

        if (normalDescriptor is null)
        {
            return null;
        }

        return ResolveNormal(normalDescriptor, context);
    }

    private Array ResolveEnumerable(Type elementType, ContextualResolutionContext context)
    {
        var services = new List<object?>();

        foreach (var descriptor in _serviceDescriptors
            .Where(descriptor => descriptor.ServiceType == elementType)
            .Where(IsNormalServiceDescriptor))
        {
            services.Add(ResolveNormal(descriptor, context));
        }

        foreach (var match in FindAllContextualRegistrations(elementType, context))
        {
            services.Add(ResolveContextual(match, context));
        }

        var array = Array.CreateInstance(elementType, services.Count);
        for (var index = 0; index < services.Count; index++)
        {
            array.SetValue(services[index], index);
        }

        return array;
    }

    private object ResolveNormal(ServiceDescriptor descriptor, ContextualResolutionContext context)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.Lifetime != ServiceLifetime.Transient)
        {
            return ResolveRootDescriptor(descriptor);
        }

        var provider = new ContextualServiceProvider(this, context);
        return TrackDisposable(CreateFromDescriptor(descriptor, provider));
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

    private object ResolveContextual(ContextualRegistrationMatch match, ContextualResolutionContext context)
    {
        if (match.Registration.Lifetime == ServiceLifetime.Transient)
        {
            return TrackDisposable(CreateContextual(match.Registration, context));
        }

        var partition = match.Registration.Lifetime == ServiceLifetime.Singleton
            ? match.Registration.Rule.ToString()
            : match.RuleMatch.InferredPartitionKey;
        var cacheKey = new ContextualCacheKey(match.Registration, partition);

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                return existing;
            }

            var created = CreateContextual(match.Registration, context);
            _cache.Add(cacheKey, created);
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
        ContextualResolutionContext context)
    {
        var matches = FindAllContextualRegistrations(serviceType, context).ToArray();
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
        ContextualResolutionContext context)
    {
        foreach (var registration in _contextualRegistrations.Where(registration => registration.ServiceType == serviceType))
        {
            var match = registration.Rule.Match(context.Groups);
            if (match is not null)
            {
                yield return new ContextualRegistrationMatch(registration, match.Value);
            }
        }
    }

    private static bool IsNormalServiceDescriptor(ServiceDescriptor descriptor)
        => descriptor.ServiceType != typeof(ContextualServiceRegistration);

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
