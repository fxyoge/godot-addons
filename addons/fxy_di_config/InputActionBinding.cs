namespace Fxyoge.DependencyInjection.Configuration;

using System;

public sealed class InputActionBinding
{
    public long KeyCode { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public static InputActionBinding FromKeyCode(long keyCode, Func<long, string>? displayNameFormatter = null)
        => new()
        {
            KeyCode = keyCode,
            DisplayName = keyCode == 0 ? "Unbound" : displayNameFormatter?.Invoke(keyCode) ?? keyCode.ToString(),
        };
}
