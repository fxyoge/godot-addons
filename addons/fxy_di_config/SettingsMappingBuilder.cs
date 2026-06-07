using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class SettingsMappingBuilder<TOptions>
    where TOptions : class, new()
{
    private readonly List<IOptionPropertyMapping<TOptions>> _mappings = new();

    public SettingsMappingBuilder(string section)
    {
        Section = section;
    }

    public string Section { get; }

    internal IReadOnlyList<IOptionPropertyMapping<TOptions>> Build() => _mappings;

    public SettingPropertyMappingBuilder<TOptions, TValue> Map<TValue>(
        Expression<Func<TOptions, TValue>> property)
    {
        if (property.Body is not MemberExpression { Member: PropertyInfo propertyInfo })
        {
            throw new ArgumentException("Mapped options members must be writable properties.", nameof(property));
        }

        if (propertyInfo.SetMethod is null)
        {
            throw new ArgumentException(
                $"Mapped options property '{propertyInfo.Name}' must have a setter.",
                nameof(property));
        }

        var getter = property.Compile();
        void Setter(TOptions options, TValue value) => propertyInfo.SetValue(options, value);

        return new SettingPropertyMappingBuilder<TOptions, TValue>(
            Section,
            ToSnakeCase(propertyInfo.Name),
            getter,
            Setter,
            mapping => _mappings.Add(mapping));
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 4);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0 && value[i - 1] != '_' && !char.IsUpper(value[i - 1]))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}

public sealed class SettingPropertyMappingBuilder<TOptions, TValue>
    where TOptions : class, new()
{
    private readonly string _section;
    private string _key;
    private readonly Func<TOptions, TValue> _getValue;
    private readonly Action<TOptions, TValue> _setValue;
    private readonly Action<IOptionPropertyMapping<TOptions>> _addMapping;
    private ConfigUiHint? _uiHint;

    internal SettingPropertyMappingBuilder(
        string section,
        string key,
        Func<TOptions, TValue> getValue,
        Action<TOptions, TValue> setValue,
        Action<IOptionPropertyMapping<TOptions>> addMapping)
    {
        _section = section;
        _key = key;
        _getValue = getValue;
        _setValue = setValue;
        _addMapping = addMapping;
    }

    public SettingPropertyMappingBuilder<TOptions, TValue> WithUi(
        string label,
        ConfigUiControl control = ConfigUiControl.Automatic,
        double? min = null,
        double? max = null,
        double? step = null)
    {
        _uiHint = new ConfigUiHint(label, control, min, max, step);
        return this;
    }

    public SettingPropertyMappingBuilder<TOptions, TValue> PersistAs(string key)
    {
        _key = key;
        return this;
    }

    public void ToUserConfig(TValue defaultValue)
    {
        Add(defaultValue, ConfigValueSource.UserConfig, runtimeBinding: null);
    }

    public void ToRuntime(IRuntimeConfigBinding<TValue> runtimeBinding, TValue fallbackDefault)
    {
        Add(fallbackDefault, runtimeBinding.Describe(_section, _key, _uiHint), runtimeBinding);
    }

    private void Add(
        TValue fallbackDefault,
        ConfigValueSource source,
        IRuntimeConfigBinding<TValue>? runtimeBinding)
    {
        Add(
            fallbackDefault,
            new ConfigEntryDescriptor(
                _section,
                _key,
                typeof(TValue),
                source,
                Writable: true,
                RuntimeMutable: true,
                RequiresRestart: false,
                _uiHint),
            runtimeBinding);
    }

    private void Add(
        TValue fallbackDefault,
        ConfigEntryDescriptor descriptor,
        IRuntimeConfigBinding<TValue>? runtimeBinding)
    {
        _addMapping(new OptionPropertyMapping<TOptions, TValue>(
            _section,
            _key,
            _getValue,
            _setValue,
            fallbackDefault,
            descriptor,
            runtimeBinding));
    }
}
