using System;
using System.Collections.Generic;
using System.Linq;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class InputActionBindings : ICloneable, IEquatable<InputActionBindings>
{
    private readonly InputBinding[] _bindings;

    public InputActionBindings()
        : this(Array.Empty<InputBinding>())
    {
    }

    public InputActionBindings(IEnumerable<InputBinding> bindings)
    {
        _bindings = bindings.ToArray();
        Bindings = Array.AsReadOnly(_bindings);
    }

    public IReadOnlyList<InputBinding> Bindings { get; }

    public string DisplayName
        => _bindings.Length == 0 ? "Unbound" : string.Join(", ", _bindings.Select(binding => binding.DisplayName));

    public object Clone() => new InputActionBindings(_bindings);

    public bool Equals(InputActionBindings? other)
        => other is not null && _bindings.SequenceEqual(other._bindings);

    public override bool Equals(object? obj)
        => obj is InputActionBindings other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var binding in _bindings)
        {
            hash.Add(binding);
        }

        return hash.ToHashCode();
    }

    public static InputActionBindings Empty { get; } = new();

    public static InputActionBindings FromKeyCode(long keyCode, Func<long, string>? displayNameFormatter = null)
        => keyCode == 0
            ? Empty
            : new InputActionBindings(new[]
            {
                new KeyInputBinding(
                    keyCode,
                    displayNameFormatter?.Invoke(keyCode) ?? keyCode.ToString()),
            });
}

public abstract record InputBinding(string DisplayName);

public sealed record KeyInputBinding(
    long KeyCode,
    string DisplayName,
    bool Ctrl = false,
    bool Alt = false,
    bool Shift = false,
    bool Meta = false) : InputBinding(DisplayName);

public sealed record MouseButtonInputBinding(
    long ButtonIndex,
    string DisplayName,
    bool Ctrl = false,
    bool Alt = false,
    bool Shift = false,
    bool Meta = false) : InputBinding(DisplayName);

public sealed record JoypadButtonInputBinding(
    long ButtonIndex,
    string DisplayName) : InputBinding(DisplayName);

public sealed record JoypadAxisInputBinding(
    long Axis,
    float AxisValue,
    string DisplayName) : InputBinding(DisplayName);
