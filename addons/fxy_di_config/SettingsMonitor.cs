using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class SettingsMonitor<TOptions> : ISettingsMonitor<TOptions>
    where TOptions : class, new()
{
    private readonly IConfigOverlayStore _store;
    private readonly IReadOnlyList<IOptionPropertyMapping<TOptions>> _mappings;
    private readonly IReadOnlyDictionary<PropertyInfo, IOptionPropertyMapping<TOptions>> _mappingsByProperty;
    private readonly object _sync = new();
    private readonly List<Action<TOptions, string>> _listeners = new();
    private TOptions _currentValue;

    public SettingsMonitor(
        IConfigOverlayStore store,
        IEnumerable<ISettingsRegistration<TOptions>> registrations)
    {
        _store = store;
        _mappings = registrations.SelectMany(registration => registration.Mappings).ToArray();
        _mappingsByProperty = _mappings.ToDictionary(mapping => mapping.Property);
        foreach (var mapping in _mappings)
        {
            mapping.CaptureDefault();
        }

        _currentValue = LoadCurrentValue(out var overlayMappings);
        Apply(_currentValue, overlayMappings);
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

        return Commit(mapping, update(current), save: true);
    }

    public ValueTask Set<TValue>(
        Expression<Func<TOptions, TValue>> property,
        TValue value)
        => Commit(GetMapping(property), value, save: true);

    public ValueTask Reset() => Reset(save: true);

    public ValueTask Save()
    {
        _store.Save();
        return ValueTask.CompletedTask;
    }

    internal ValueTask Commit<TValue>(
        IOptionPropertyMapping<TOptions> mapping,
        TValue value,
        bool save,
        bool captureOverlay = true)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            var next = CloneMapped(_currentValue);
            mapping.SetValue(next, value);
            var overlaySnapshot = _store.CreateSnapshot();

            try
            {
                if (captureOverlay)
                {
                    mapping.CaptureOverlay(next, _store);
                }

                if (save)
                {
                    _store.Save();
                }
            }
            catch
            {
                _store.RestoreSnapshot(overlaySnapshot);
                throw;
            }

            var runtimeSnapshots = CaptureRuntime(new[] { mapping });
            try
            {
                mapping.Apply(next);
            }
            catch
            {
                RestoreRuntime(runtimeSnapshots);
                RestoreOverlay(overlaySnapshot, save);
                throw;
            }

            _currentValue = CloneMapped(next);
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }

        return ValueTask.CompletedTask;
    }

    internal ValueTask Commit(
        TOptions value,
        IEnumerable<IOptionPropertyMapping<TOptions>> changedMappings,
        bool save)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            var next = CloneMapped(_currentValue);
            var changed = changedMappings.ToArray();
            var overlaySnapshot = _store.CreateSnapshot();

            try
            {
                foreach (var mapping in changed)
                {
                    CopyMappedValue(mapping, value, next);
                    mapping.CaptureOverlay(next, _store);
                }

                if (save)
                {
                    _store.Save();
                }
            }
            catch
            {
                _store.RestoreSnapshot(overlaySnapshot);
                throw;
            }

            var runtimeSnapshots = CaptureRuntime(changed);
            try
            {
                foreach (var mapping in changed)
                {
                    mapping.Apply(next);
                }
            }
            catch
            {
                RestoreRuntime(runtimeSnapshots);
                RestoreOverlay(overlaySnapshot, save);
                throw;
            }

            _currentValue = CloneMapped(next);
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }

        return ValueTask.CompletedTask;
    }

    internal ValueTask Reset(bool save)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions next;
        TOptions publishedValue;

        lock (_sync)
        {
            var overlaySnapshot = _store.CreateSnapshot();

            try
            {
                foreach (var mapping in _mappings)
                {
                    mapping.ResetOverlay(_store);
                }

                if (save)
                {
                    _store.Save();
                }
            }
            catch
            {
                _store.RestoreSnapshot(overlaySnapshot);
                throw;
            }

            next = LoadCurrentValue(out _);
            var runtimeSnapshots = CaptureRuntime(_mappings);
            try
            {
                Apply(next);
            }
            catch
            {
                RestoreRuntime(runtimeSnapshots);
                RestoreOverlay(overlaySnapshot, save);
                throw;
            }

            _currentValue = CloneMapped(next);
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }

        return ValueTask.CompletedTask;
    }

    internal IPreparedSettingsCommit PrepareCommit(
        TOptions value,
        IEnumerable<IOptionPropertyMapping<TOptions>> changedMappings)
    {
        lock (_sync)
        {
            var next = CloneMapped(_currentValue);
            var changed = changedMappings.ToArray();

            foreach (var mapping in changed)
            {
                CopyMappedValue(mapping, value, next);
                mapping.CaptureOverlay(next, _store);
            }

            return new PreparedSettingsCommit(this, next, changed);
        }
    }

    internal IPreparedSettingsCommit PrepareReset()
    {
        lock (_sync)
        {
            foreach (var mapping in _mappings)
            {
                mapping.ResetOverlay(_store);
            }

            var next = LoadCurrentValue(out _);
            return new PreparedSettingsCommit(this, next, _mappings);
        }
    }

    private TOptions LoadCurrentValue(out IReadOnlyList<IOptionPropertyMapping<TOptions>> overlayMappings)
    {
        var options = new TOptions();
        var overlays = new List<IOptionPropertyMapping<TOptions>>();

        foreach (var mapping in _mappings)
        {
            mapping.LoadDefault(options);
            if (mapping.LoadOverlay(options, _store))
            {
                overlays.Add(mapping);
            }
        }

        overlayMappings = overlays;
        return options;
    }

    private void Apply(TOptions options)
        => Apply(options, _mappings);

    private static void Apply(
        TOptions options,
        IEnumerable<IOptionPropertyMapping<TOptions>> mappings)
    {
        foreach (var mapping in mappings)
        {
            mapping.Apply(options);
        }
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

    private static void CopyMappedValue(
        IOptionPropertyMapping<TOptions> mapping,
        TOptions source,
        TOptions target)
    {
        mapping.CopyValue(source, target);
    }

    private IReadOnlyList<RuntimeSnapshot> CaptureRuntime(
        IEnumerable<IOptionPropertyMapping<TOptions>> mappings)
        => mappings
            .Select(mapping => new RuntimeSnapshot(mapping, mapping.CaptureRuntime()))
            .ToArray();

    private static void RestoreRuntime(IEnumerable<RuntimeSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots.Reverse())
        {
            snapshot.Mapping.RestoreRuntime(snapshot.Value);
        }
    }

    private void RestoreOverlay(IConfigOverlayStoreSnapshot snapshot, bool save)
    {
        _store.RestoreSnapshot(snapshot);
        if (save)
        {
            _store.Save();
        }
    }

    private void PublishPrepared(TOptions next)
    {
        IReadOnlyList<Action<TOptions, string>> listeners;
        TOptions publishedValue;

        lock (_sync)
        {
            _currentValue = CloneMapped(next);
            publishedValue = CloneMapped(_currentValue);
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(CloneMapped(publishedValue), Options.DefaultName);
        }
    }

    private sealed record RuntimeSnapshot(
        IOptionPropertyMapping<TOptions> Mapping,
        object? Value);

    private sealed class PreparedSettingsCommit : IPreparedSettingsCommit
    {
        private readonly SettingsMonitor<TOptions> _monitor;
        private readonly TOptions _next;
        private readonly IReadOnlyList<IOptionPropertyMapping<TOptions>> _changedMappings;
        private IReadOnlyList<RuntimeSnapshot>? _runtimeSnapshots;

        public PreparedSettingsCommit(
            SettingsMonitor<TOptions> monitor,
            TOptions next,
            IReadOnlyList<IOptionPropertyMapping<TOptions>> changedMappings)
        {
            _monitor = monitor;
            _next = next;
            _changedMappings = changedMappings;
        }

        public void ApplyRuntime()
        {
            _runtimeSnapshots = _monitor.CaptureRuntime(_changedMappings);

            try
            {
                foreach (var mapping in _changedMappings)
                {
                    mapping.Apply(_next);
                }
            }
            catch
            {
                RollbackRuntime();
                throw;
            }
        }

        public void RollbackRuntime()
        {
            if (_runtimeSnapshots is not null)
            {
                RestoreRuntime(_runtimeSnapshots);
            }
        }

        public void Publish() => _monitor.PublishPrepared(_next);
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
