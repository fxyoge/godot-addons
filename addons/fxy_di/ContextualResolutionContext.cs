using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Fxyoge.DependencyInjection;

internal sealed class ContextualResolutionContext
{
    public ContextualResolutionContext(IEnumerable<string> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        Groups = groups.ToImmutableHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> Groups { get; }
}
