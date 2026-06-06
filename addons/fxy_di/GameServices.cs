using System;
using Godot;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public partial class GameServices : Node
{
    private ServiceProvider? _provider;

    public IServiceProvider Provider => _provider ??= CreateProvider();

    public T GetRequiredService<T>()
        where T : notnull
        => Provider.GetRequiredService<T>();

    public T? GetService<T>()
        where T : class
        => Provider.GetService<T>();

    public IServiceScope CreateScope() => Provider.CreateScope();

    public override void _ExitTree()
    {
        _provider?.Dispose();
        _provider = null;
    }

    private ServiceProvider CreateProvider()
    {
        ServiceCollection services = new();

        services.AddSingleton(this);
        services.AddSingleton(GetTree());

        foreach (var startup in StartupDiscovery.CreateStartups())
        {
            startup.ConfigureServices(services);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
