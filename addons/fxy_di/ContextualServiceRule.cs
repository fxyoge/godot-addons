using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Fxyoge.DependencyInjection;

internal sealed class ContextualServiceRule
{
    private ContextualServiceRule(ImmutableArray<ContextualServiceRuleTerm> terms)
    {
        Terms = terms;
    }

    public ImmutableArray<ContextualServiceRuleTerm> Terms { get; }

    public static ContextualServiceRule Parse(IEnumerable<string> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var terms = rules
            .Select(ContextualServiceRuleTerm.Parse)
            .ToImmutableArray();

        if (terms.Length == 0)
        {
            throw new ArgumentException("A contextual service rule must contain at least one term.", nameof(rules));
        }

        var duplicate = terms
            .GroupBy(term => (term.IsNegative, term.Prefix, term.IsWildcard))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Contextual service rule contains duplicate term '{duplicate.First()}'.",
                nameof(rules));
        }

        return new ContextualServiceRule(terms);
    }

    public ContextualServiceRuleMatch? Match(IReadOnlySet<string> groups)
    {
        var matchedGroups = ImmutableArray.CreateBuilder<string>();

        foreach (var term in Terms)
        {
            var matchedGroup = term.Match(groups);
            if (matchedGroup is null && !term.IsNegative)
            {
                return null;
            }

            if (matchedGroup is not null && term.IsNegative)
            {
                return null;
            }

            if (matchedGroup is not null)
            {
                matchedGroups.Add(matchedGroup);
            }
        }

        return new ContextualServiceRuleMatch(
            string.Join("|", Terms.Select(term => term.ToString())),
            matchedGroups.ToImmutable());
    }

    public ContextualServiceRuleSpecificity Specificity
        => new(
            Terms.Length,
            Terms.Count(term => !term.IsWildcard),
            Terms.Count(term => !term.IsNegative));

    public override string ToString() => string.Join(", ", Terms.Select(term => term.ToString()));
}

internal readonly record struct ContextualServiceRuleMatch(
    string RuleIdentity,
    ImmutableArray<string> MatchedGroups)
{
    public string InferredPartitionKey
    {
        get
        {
            if (MatchedGroups.Length == 0)
            {
                return RuleIdentity;
            }

            return $"{RuleIdentity}::{string.Join("|", MatchedGroups.Order(StringComparer.Ordinal))}";
        }
    }
}

internal readonly record struct ContextualServiceRuleSpecificity(
    int TermCount,
    int ExactTermCount,
    int PositiveTermCount) : IComparable<ContextualServiceRuleSpecificity>
{
    public int CompareTo(ContextualServiceRuleSpecificity other)
    {
        var termComparison = TermCount.CompareTo(other.TermCount);
        if (termComparison != 0)
        {
            return termComparison;
        }

        var exactComparison = ExactTermCount.CompareTo(other.ExactTermCount);
        if (exactComparison != 0)
        {
            return exactComparison;
        }

        return PositiveTermCount.CompareTo(other.PositiveTermCount);
    }
}
