using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public partial class GameServices : Node
{
    private ServiceProvider? _provider;
    private ContextualServiceResolver? _contextualResolver;
    private ImmutableArray<ServiceDescriptor> _serviceDescriptors;

    public IServiceProvider Provider => _provider ??= CreateProvider();

    public T GetRequiredService<T>()
        where T : notnull
        => Provider.GetRequiredService<T>();

    public T? GetService<T>()
        where T : class
        => Provider.GetService<T>();

    public T GetRequiredService<T>(Node node)
        where T : notnull
        => (T)(ContextualResolver.GetService(typeof(T), CreateContext(node))
            ?? throw new InvalidOperationException($"No service for type '{typeof(T)}' has been registered."));

    public object GetRequiredService(Node node, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return ContextualResolver.GetService(serviceType, CreateContext(node))
            ?? throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.");
    }

    public T? GetService<T>(Node node)
        where T : class
        => (T?)ContextualResolver.GetService(typeof(T), CreateContext(node));

    public object? GetService(Node node, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return ContextualResolver.GetService(serviceType, CreateContext(node));
    }

    public IServiceScope CreateScope() => Provider.CreateScope();

    public void DisposeContext(Node node) => ContextualResolver.DisposeContext(CreateContext(node));

    public ServiceResolutionDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        => ContextualResolver.CreateDiagnosticsSnapshot();

    public void ClearDiagnostics() => ContextualResolver.ClearDiagnostics();

    public override void _ExitTree()
    {
        _contextualResolver?.Dispose();
        _contextualResolver = null;
        _provider?.Dispose();
        _provider = null;
    }

    private ContextualServiceResolver ContextualResolver
        => _contextualResolver ??= new ContextualServiceResolver(
            (ServiceProvider)Provider,
            _serviceDescriptors);

    private static ContextualResolutionContext CreateContext(Node node)
        => new(GetGroups(node));

    private static IEnumerable<string> GetGroups(Node node)
        => node.GetGroups().Select(group => group.ToString());

    private ServiceProvider CreateProvider()
    {
        ServiceCollection services = new();
        ConfigureServices(services);
        _serviceDescriptors = services.ToImmutableArray();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = true,
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton(GetTree());

        foreach (var startup in StartupDiscovery.CreateStartups())
        {
            startup.ConfigureServices(services);
        }
    }
}
