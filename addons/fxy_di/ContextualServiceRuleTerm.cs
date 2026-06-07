using System;
using System.Collections.Generic;
using System.Linq;

namespace Fxyoge.DependencyInjection;

internal sealed class ContextualServiceRuleTerm
{
    private ContextualServiceRuleTerm(bool isNegative, string value, bool isWildcard)
    {
        IsNegative = isNegative;
        Value = value;
        IsWildcard = isWildcard;
        Prefix = isWildcard ? value[..^1] : value;
    }

    public bool IsNegative { get; }

    public bool IsWildcard { get; }

    public string Prefix { get; }

    private string Value { get; }

    public static ContextualServiceRuleTerm Parse(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            throw new ArgumentException("Contextual service rule terms cannot be empty.", nameof(rule));
        }

        var trimmed = rule.Trim();
        var isNegative = trimmed.StartsWith('!');
        var value = isNegative ? trimmed[1..] : trimmed;

        if (value.Length == 0)
        {
            throw new ArgumentException("Contextual service rule terms cannot contain only '!'.", nameof(rule));
        }

        var wildcardIndex = value.IndexOf('*', StringComparison.Ordinal);
        if (wildcardIndex >= 0 && wildcardIndex != value.Length - 1)
        {
            throw new ArgumentException(
                $"Contextual service rule term '{rule}' can only use '*' at the end.",
                nameof(rule));
        }

        var isWildcard = wildcardIndex == value.Length - 1;
        if (isWildcard && value.Length == 1)
        {
            throw new ArgumentException(
                $"Contextual service rule term '{rule}' must include a prefix before '*'.",
                nameof(rule));
        }

        return new ContextualServiceRuleTerm(isNegative, value, isWildcard);
    }

    public string? Match(IReadOnlySet<string> groups)
    {
        if (!IsWildcard)
        {
            return groups.Contains(Value) ? Value : null;
        }

        return groups
            .Where(group => group.StartsWith(Prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public override string ToString() => IsNegative ? $"!{Value}" : Value;
}
