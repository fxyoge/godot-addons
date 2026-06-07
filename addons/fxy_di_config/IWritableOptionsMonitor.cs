using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public interface IWritableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    IDisposable OnChange(Action<TOptions> listener);

    ValueTask Update(Action<TOptions> update);

    ValueTask Set(TOptions value);

    ValueTask Reset();

    ValueTask Save();

    IWritableOptionsSession<TOptions> CreateSession();
}

public interface IWritableOptionsSession<TOptions>
    where TOptions : class, new()
{
    TOptions Value { get; }

    ValueTask Apply();

    ValueTask Save();

    ValueTask Reset();
}
