using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class WritableOptionsMonitor<TOptions> : IWritableOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    private readonly IConfigOverlayStore _store;
    private readonly IReadOnlyList<IOptionPropertyMapping<TOptions>> _mappings;
    private readonly object _sync = new();
    private readonly List<Action<TOptions, string>> _listeners = new();
    private TOptions _currentValue;

    public WritableOptionsMonitor(
        IConfigOverlayStore store,
        IEnumerable<IWritableOptionsRegistration<TOptions>> registrations)
    {
        _store = store;
        _mappings = registrations.SelectMany(registration => registration.Mappings).ToArray();
        _currentValue = LoadCurrentValue();
        Apply(_currentValue);
    }

    public TOptions CurrentValue
    {
        get
        {
            lock (_sync)
            {
                return Clone(_currentValue);
            }
        }
    }

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TOptions> listener)
        => OnChange((options, _) => listener(options));

    public IDisposable OnChange(Action<TOptions, string> listener)
    {
        lock (_sync)
        {
            _listeners.Add(listener);
        }

        return new ListenerSubscription(() =>
        {
            lock (_sync)
            {
                _listeners.Remove(listener);
            }
        });
    }

    public ValueTask Update(Action<TOptions> update)
    {
        var next = CurrentValue;
        update(next);
        return Commit(next, save: true);
    }

    public ValueTask Set(TOptions value) => Commit(Clone(value), save: true);

    public ValueTask Reset()
    {
        TOptions next;

        lock (_sync)
        {
            foreach (var mapping in _mappings)
            {
                mapping.ResetOverlay(_store);
            }

            next = LoadCurrentValue();
        }

        return Commit(next, save: true, captureOverlay: false);
    }

    public ValueTask Save()
    {
        _store.Save();
        return ValueTask.CompletedTask;
    }

    public IWritableOptionsSession<TOptions> CreateSession() => new WritableOptionsSession<TOptions>(this, CurrentValue);

    internal ValueTask Commit(TOptions value, bool save, bool captureOverlay = true)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            if (captureOverlay)
            {
                foreach (var mapping in _mappings)
                {
                    mapping.CaptureOverlay(value, _store);
                }
            }

            Apply(value);

            if (save)
            {
                _store.Save();
            }

            _currentValue = Clone(value);
            publishedValue = Clone(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(Clone(publishedValue), Options.DefaultName);
        }

        return ValueTask.CompletedTask;
    }

    private TOptions LoadCurrentValue()
    {
        var options = new TOptions();

        foreach (var mapping in _mappings)
        {
            mapping.LoadDefault(options);
            mapping.LoadOverlay(options, _store);
        }

        return options;
    }

    private void Apply(TOptions options)
    {
        foreach (var mapping in _mappings)
        {
            mapping.Apply(options);
        }
    }

    private static TOptions Clone(TOptions value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<TOptions>(json) ?? new TOptions();
    }

    private sealed class ListenerSubscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public ListenerSubscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}

internal sealed class WritableOptionsSession<TOptions> : IWritableOptionsSession<TOptions>
    where TOptions : class, new()
{
    private readonly WritableOptionsMonitor<TOptions> _monitor;

    public WritableOptionsSession(WritableOptionsMonitor<TOptions> monitor, TOptions value)
    {
        _monitor = monitor;
        Value = value;
    }

    public TOptions Value { get; private set; }

    public ValueTask Apply() => _monitor.Commit(Value, save: false);

    public ValueTask Save() => _monitor.Commit(Value, save: true);

    public ValueTask Reset()
    {
        var result = _monitor.Reset();
        Value = _monitor.CurrentValue;
        return result;
    }
}
