using System;

namespace Fxyoge.DependencyInjection.Configuration;

internal static class ConfigMappedValue
{
    public static void EnsureSupported<TValue>(string section, string key)
    {
        var valueType = typeof(TValue);
        if (valueType.IsValueType
            || valueType == typeof(string)
            || typeof(ICloneable).IsAssignableFrom(valueType))
        {
            return;
        }

        throw new NotSupportedException(
            $"Mapped setting '{section}/{key}' uses reference type '{valueType.FullName}'. " +
            "Use an immutable primitive/value type or implement ICloneable.");
    }

    public static TValue Copy<TValue>(TValue value)
    {
        if (value is null)
        {
            return value;
        }

        var valueType = typeof(TValue);
        if (valueType.IsValueType || valueType == typeof(string))
        {
            return value;
        }

        if (value is ICloneable cloneable)
        {
            return (TValue)cloneable.Clone();
        }

        throw new NotSupportedException(
            $"Mapped setting value '{valueType.FullName}' cannot be copied. " +
            "Use an immutable primitive/value type or implement ICloneable.");
    }
}
