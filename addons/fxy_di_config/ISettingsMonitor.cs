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
}

public interface ISettingsTransaction
{
    bool HasChanges { get; }

    ValueTask Save();

    void Abandon();
}
