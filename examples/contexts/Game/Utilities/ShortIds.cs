using System;

namespace ContextsExample.Game;

internal static class ShortIds
{
    public static string New() => Guid.NewGuid().ToString("N")[..6];
}
