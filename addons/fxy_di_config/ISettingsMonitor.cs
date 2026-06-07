using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public interface ISettingsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    IDisposable OnChange(Action<TOptions> listener);

    ValueTask Update(Action<TOptions> update);

    ValueTask Set(TOptions value);

    ValueTask Reset();

    ValueTask Save();

    ISettingsSession<TOptions> CreateSession();
}

public interface ISettingsSession<TOptions>
    where TOptions : class, new()
{
    TOptions Value { get; }

    ValueTask Apply();

    ValueTask Save();

    ValueTask Reset();
}
