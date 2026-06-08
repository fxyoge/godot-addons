using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Fxyoge.DependencyInjection.Configuration;

internal static class SettingsPropertyExpression
{
    public static PropertyInfo GetProperty<TOptions, TValue>(
        Expression<Func<TOptions, TValue>> property)
    {
        if (property.Body is not MemberExpression { Member: PropertyInfo propertyInfo })
        {
            throw new ArgumentException("Mapped options members must be properties.", nameof(property));
        }

        return propertyInfo;
    }
}
