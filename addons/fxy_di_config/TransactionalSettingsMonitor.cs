using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class TransactionalSettingsMonitor<TOptions> :
    ISettingsMonitor<TOptions>,
    ITransactionalSettingsParticipant,
    IDisposable
    where TOptions : class, new()
{
    private readonly SettingsMonitor<TOptions> _liveMonitor;
    private readonly IReadOnlyList<IOptionPropertyMapping<TOptions>> _mappings;
    private readonly IReadOnlyDictionary<PropertyInfo, IOptionPropertyMapping<TOptions>> _mappingsByProperty;
    private readonly SettingsTransaction _transaction;
    private readonly IDisposable _liveSubscription;
    private readonly object _sync = new();
    private readonly List<Action<TOptions, string>> _listeners = new();
    private readonly HashSet<IOptionPropertyMapping<TOptions>> _changedMappings = new();
    private TOptions _currentValue;
    private bool _pendingReset;

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
        _mappingsByProperty = _mappings.ToDictionary(mapping => mapping.Property);
        _currentValue = CloneMapped(_liveMonitor.CurrentValue);
        _liveSubscription = _liveMonitor.OnChange(SyncFromLive);
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
                return _pendingReset || _changedMappings.Count > 0;
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

    public ValueTask Update<TValue>(
        Expression<Func<TOptions, TValue>> property,
        Func<TValue, TValue> update)
    {
        var mapping = GetMapping(property);
        TValue current;

        lock (_sync)
        {
            current = mapping.GetValue<TValue>(_currentValue);
        }

        return Set(property, update(current));
    }

    public ValueTask Set<TValue>(
        Expression<Func<TOptions, TValue>> property,
        TValue value)
    {
        Publish(GetMapping(property), value);
        return ValueTask.CompletedTask;
    }

    public ValueTask Reset()
    {
        PublishReset(LoadDefaults());
        return ValueTask.CompletedTask;
    }

    public ValueTask Save() => _transaction.Save();

    public async ValueTask SaveToLive()
    {
        TOptions value;
        bool pendingReset;
        IOptionPropertyMapping<TOptions>[] changedMappings;

        lock (_sync)
        {
            value = CloneMapped(_currentValue);
            pendingReset = _pendingReset;
            changedMappings = _changedMappings.ToArray();
        }

        if (!pendingReset && changedMappings.Length == 0)
        {
            return;
        }

        if (pendingReset)
        {
            await _liveMonitor.Reset(save: false);
        }

        if (changedMappings.Length > 0)
        {
            await _liveMonitor.Commit(value, changedMappings, save: false);
        }

        lock (_sync)
        {
            _currentValue = CloneMapped(_liveMonitor.CurrentValue);
            _pendingReset = false;
            _changedMappings.Clear();
        }
    }

    public void Abandon()
    {
        PublishClean(_liveMonitor.CurrentValue);
    }

    public void Dispose()
    {
        _liveSubscription.Dispose();
    }

    private void SyncFromLive(TOptions value)
    {
        lock (_sync)
        {
            if (_pendingReset || _changedMappings.Count > 0)
            {
                return;
            }
        }

        PublishClean(value);
    }

    private void Publish<TValue>(IOptionPropertyMapping<TOptions> mapping, TValue value)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            var next = CloneMapped(_currentValue);
            mapping.SetValue(next, value);
            _currentValue = CloneMapped(next);
            _changedMappings.Add(mapping);
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }
    }

    private void PublishReset(TOptions value)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            _currentValue = CloneMapped(value);
            _pendingReset = true;
            _changedMappings.Clear();
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }
    }

    private void PublishClean(TOptions value)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            _currentValue = CloneMapped(value);
            _pendingReset = false;
            _changedMappings.Clear();
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

    private IOptionPropertyMapping<TOptions> GetMapping<TValue>(
        Expression<Func<TOptions, TValue>> property)
    {
        var propertyInfo = SettingsPropertyExpression.GetProperty(property);
        if (_mappingsByProperty.TryGetValue(propertyInfo, out var mapping))
        {
            return mapping;
        }

        throw new InvalidOperationException(
            $"Options property '{typeof(TOptions).FullName}.{propertyInfo.Name}' is not mapped.");
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
