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
    private bool _isDisposed;

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
        _transaction.Enlist(this, HasChangesForTransaction, PrepareSaveToLive, AbandonForTransaction);
    }

    public TOptions CurrentValue
    {
        get
        {
            lock (_sync)
            {
                ThrowIfDisposed();
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
                ThrowIfDisposed();
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
            ThrowIfDisposed();
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
        ThrowIfDisposed();
        var mapping = GetMapping(property);
        TValue current;

        lock (_sync)
        {
            ThrowIfDisposed();
            current = mapping.GetValue<TValue>(_currentValue);
        }

        return Set(property, update(current));
    }

    public ValueTask Set<TValue>(
        Expression<Func<TOptions, TValue>> property,
        TValue value)
    {
        ThrowIfDisposed();
        Publish(GetMapping(property), value);
        return ValueTask.CompletedTask;
    }

    public ValueTask Reset()
    {
        ThrowIfDisposed();
        PublishReset(LoadDefaults());
        return ValueTask.CompletedTask;
    }

    public ValueTask Save()
    {
        ThrowIfDisposed();
        return _transaction.Save();
    }

    private IPreparedSettingsCommit PrepareSaveToLive()
    {
        TOptions value;
        bool pendingReset;
        IOptionPropertyMapping<TOptions>[] changedMappings;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return PreparedSettingsCommit.Empty;
            }

            value = CloneMapped(_currentValue);
            pendingReset = _pendingReset;
            changedMappings = _changedMappings.ToArray();
        }

        if (!pendingReset && changedMappings.Length == 0)
        {
            return PreparedSettingsCommit.Empty;
        }

        IPreparedSettingsCommit preparedCommit;
        if (pendingReset)
        {
            preparedCommit = _liveMonitor.PrepareReset();
        }
        else
        {
            preparedCommit = _liveMonitor.PrepareCommit(value, changedMappings);
        }

        return new TransactionalPreparedSettingsCommit(
            preparedCommit,
            () =>
            {
                lock (_sync)
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    _currentValue = CloneMapped(_liveMonitor.CurrentValue);
                    _pendingReset = false;
                    _changedMappings.Clear();
                }
            });
    }

    public void Abandon()
    {
        ThrowIfDisposed();
        PublishClean(_liveMonitor.CurrentValue);
    }

    private bool HasChangesForTransaction()
    {
        lock (_sync)
        {
            return !_isDisposed && (_pendingReset || _changedMappings.Count > 0);
        }
    }

    private void AbandonForTransaction()
    {
        PublishClean(_liveMonitor.CurrentValue);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _listeners.Clear();
            _changedMappings.Clear();
            _pendingReset = false;
        }

        _liveSubscription.Dispose();
        _transaction.Unenlist(this);
    }

    private void SyncFromLive(TOptions value)
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            if (_isDisposed)
            {
                return;
            }

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

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
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

    private sealed class TransactionalPreparedSettingsCommit : IPreparedSettingsCommit
    {
        private readonly IPreparedSettingsCommit _inner;
        private readonly Action _publishDraftState;

        public TransactionalPreparedSettingsCommit(
            IPreparedSettingsCommit inner,
            Action publishDraftState)
        {
            _inner = inner;
            _publishDraftState = publishDraftState;
        }

        public void ApplyRuntime() => _inner.ApplyRuntime();

        public void RollbackRuntime() => _inner.RollbackRuntime();

        public void Publish()
        {
            _inner.Publish();
            _publishDraftState();
        }
    }
}
