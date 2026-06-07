using System;
using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public sealed record ContextualServiceMatch(
    Type ServiceType,
    Type ImplementationType,
    ServiceLifetime Lifetime,
    string Rule);
