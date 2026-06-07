namespace Fxyoge.DependencyInjection.Configuration;

using System;

public readonly record struct InputActionBinding(long KeyCode, string DisplayName)
{
    public InputActionBinding()
        : this(0, "Unbound")
    {
    }

    public static InputActionBinding FromKeyCode(long keyCode, Func<long, string>? displayNameFormatter = null)
        => new(keyCode, keyCode == 0 ? "Unbound" : displayNameFormatter?.Invoke(keyCode) ?? keyCode.ToString());
}
