using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class TransactionalSettingsMonitor<TOptions> :
    ISettingsMonitor<TOptions>,
    ITransactionalSettingsParticipant
    where TOptions : class, new()
{
    private readonly SettingsMonitor<TOptions> _liveMonitor;
    private readonly IReadOnlyList<IOptionPropertyMapping<TOptions>> _mappings;
    private readonly SettingsTransaction _transaction;
    private readonly object _sync = new();
    private readonly List<Action<TOptions, string>> _listeners = new();
    private TOptions _currentValue;
    private bool _pendingReset;
    private bool _hasValueChanges;

    public TransactionalSettingsMonitor(
        SettingsMonitor<TOptions> liveMonitor,
        ISettingsTransaction transaction,
        IEnumerable<ISettingsRegistration<TOptions>> registrations)
    {
        if (transaction is not SettingsTransaction settingsTransaction)
        {
            throw new InvalidOperationException(
                $"Transactional settings require the default '{nameof(SettingsTransaction)}' service.");
        }

        _liveMonitor = liveMonitor;
        _transaction = settingsTransaction;
        _mappings = registrations.SelectMany(registration => registration.Mappings).ToArray();
        _currentValue = CloneMapped(_liveMonitor.CurrentValue);
        _transaction.Enlist(this);
    }

    public TOptions CurrentValue
    {
        get
        {
            lock (_sync)
            {
                return CloneMapped(_currentValue);
            }
        }
    }

    public bool HasChanges
    {
        get
        {
            lock (_sync)
            {
                return _pendingReset || _hasValueChanges;
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
        return Set(next);
    }

    public ValueTask Set(TOptions value)
    {
        Publish(value, pendingReset: _pendingReset, hasValueChanges: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask Reset()
    {
        Publish(LoadDefaults(), pendingReset: true, hasValueChanges: false);
        return ValueTask.CompletedTask;
    }

    public ValueTask Save() => _transaction.Save();

    public async ValueTask SaveToLive()
    {
        TOptions value;
        bool pendingReset;
        bool hasValueChanges;

        lock (_sync)
        {
            value = CloneMapped(_currentValue);
            pendingReset = _pendingReset;
            hasValueChanges = _hasValueChanges;
        }

        if (!pendingReset && !hasValueChanges)
        {
            return;
        }

        if (pendingReset)
        {
            await _liveMonitor.Reset(save: false);
        }

        if (hasValueChanges)
        {
            await _liveMonitor.Commit(value, save: false);
        }

        lock (_sync)
        {
            _currentValue = CloneMapped(_liveMonitor.CurrentValue);
            _pendingReset = false;
            _hasValueChanges = false;
        }
    }

    public void Abandon()
    {
        Publish(_liveMonitor.CurrentValue, pendingReset: false, hasValueChanges: false);
    }

    private void Publish(TOptions value, bool pendingReset, bool hasValueChanges)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            _currentValue = CloneMapped(value);
            _pendingReset = pendingReset;
            _hasValueChanges = hasValueChanges;
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }
    }

    private TOptions LoadDefaults()
    {
        var options = new TOptions();

        foreach (var mapping in _mappings)
        {
            mapping.LoadDefault(options);
        }

        return options;
    }

    private TOptions CloneMapped(TOptions value)
    {
        var clone = new TOptions();

        foreach (var mapping in _mappings)
        {
            mapping.CopyValue(value, clone);
        }

        return clone;
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
