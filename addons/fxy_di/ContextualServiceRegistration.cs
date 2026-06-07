using System;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

internal sealed class ContextualServiceRegistration
{
    public ContextualServiceRegistration(
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime,
        ContextualServiceRule rule)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        Rule = rule;
    }

    public Type ServiceType { get; }

    public Type ImplementationType { get; }

    public ServiceLifetime Lifetime { get; }

    public ContextualServiceRule Rule { get; }
}
