using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public interface ISettingsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    IDisposable OnChange(Action<TOptions> listener);

    ValueTask Update<TValue>(
        Expression<Func<TOptions, TValue>> property,
        Func<TValue, TValue> update);

    ValueTask Set<TValue>(
        Expression<Func<TOptions, TValue>> property,
        TValue value);

    ValueTask Reset();

    ValueTask Save();
}

public interface ISettingsTransaction
{
    bool HasChanges { get; }

    ValueTask Save();

    void Abandon();
}
