using System;
using System.Collections.Generic;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class InputActionBindingConfigValueCodec : IConfigValueCodec<InputActionBindings>
{
    public static InputActionBindingConfigValueCodec Instance { get; } = new();

    private readonly Func<InputBinding, string>? _displayNameFormatter;

    public InputActionBindingConfigValueCodec(Func<InputBinding, string>? displayNameFormatter = null)
    {
        _displayNameFormatter = displayNameFormatter;
    }

    public bool TryRead(
        IConfigOverlayStore store,
        string section,
        string key,
        out InputActionBindings value)
    {
        var bindings = new List<InputBinding>();

        for (var index = 0; ; index++)
        {
            var prefix = GetBindingPrefix(key, index);
            if (!store.TryGet<string>(section, $"{prefix}/type", out var type))
            {
                break;
            }

            if (index == 0 && type == "none")
            {
                value = InputActionBindings.Empty;
                return true;
            }

            if (TryReadBinding(store, section, prefix, type, out var binding))
            {
                bindings.Add(binding);
            }
        }

        if (bindings.Count == 0)
        {
            value = default!;
            return false;
        }

        value = new InputActionBindings(bindings);
        return true;
    }

    public void Write(IConfigOverlayStore store, string section, string key, InputActionBindings value)
    {
        Remove(store, section, key);

        if (value.Bindings.Count == 0)
        {
            store.Set(section, $"{GetBindingPrefix(key, 0)}/type", "none");
            return;
        }

        for (var index = 0; index < value.Bindings.Count; index++)
        {
            WriteBinding(store, section, GetBindingPrefix(key, index), value.Bindings[index]);
        }
    }

    public void Remove(IConfigOverlayStore store, string section, string key)
    {
        for (var index = 0; ; index++)
        {
            var prefix = GetBindingPrefix(key, index);
            if (!store.TryGet<string>(section, $"{prefix}/type", out _))
            {
                return;
            }

            RemoveBinding(store, section, prefix);
        }
    }

    private bool TryReadBinding(
        IConfigOverlayStore store,
        string section,
        string prefix,
        string type,
        out InputBinding binding)
    {
        binding = null!;

        switch (type)
        {
            case "key":
                if (!store.TryGet<long>(section, $"{prefix}/key_code", out var keyCode))
                {
                    return false;
                }

                binding = WithDisplayName(new KeyInputBinding(
                    keyCode,
                    keyCode.ToString(),
                    ReadBool(store, section, prefix, "ctrl"),
                    ReadBool(store, section, prefix, "alt"),
                    ReadBool(store, section, prefix, "shift"),
                    ReadBool(store, section, prefix, "meta")));
                return true;

            case "mouse_button":
                if (!store.TryGet<long>(section, $"{prefix}/button_index", out var buttonIndex))
                {
                    return false;
                }

                binding = WithDisplayName(new MouseButtonInputBinding(
                    buttonIndex,
                    $"Mouse {buttonIndex}",
                    ReadBool(store, section, prefix, "ctrl"),
                    ReadBool(store, section, prefix, "alt"),
                    ReadBool(store, section, prefix, "shift"),
                    ReadBool(store, section, prefix, "meta")));
                return true;

            case "joypad_button":
                if (!store.TryGet<long>(section, $"{prefix}/button_index", out var joypadButton))
                {
                    return false;
                }

                binding = WithDisplayName(new JoypadButtonInputBinding(
                    joypadButton,
                    $"Joypad Button {joypadButton}"));
                return true;

            case "joypad_axis":
                if (!store.TryGet<long>(section, $"{prefix}/axis", out var axis)
                    || !store.TryGet<float>(section, $"{prefix}/axis_value", out var axisValue))
                {
                    return false;
                }

                binding = WithDisplayName(new JoypadAxisInputBinding(
                    axis,
                    axisValue,
                    $"Joypad Axis {axis} {(axisValue >= 0 ? "+" : "-")}"));
                return true;

            default:
                return false;
        }
    }

    private void WriteBinding(IConfigOverlayStore store, string section, string prefix, InputBinding binding)
    {
        switch (binding)
        {
            case KeyInputBinding key:
                store.Set(section, $"{prefix}/type", "key");
                store.Set(section, $"{prefix}/key_code", key.KeyCode);
                WriteModifiers(store, section, prefix, key.Ctrl, key.Alt, key.Shift, key.Meta);
                break;

            case MouseButtonInputBinding mouse:
                store.Set(section, $"{prefix}/type", "mouse_button");
                store.Set(section, $"{prefix}/button_index", mouse.ButtonIndex);
                WriteModifiers(store, section, prefix, mouse.Ctrl, mouse.Alt, mouse.Shift, mouse.Meta);
                break;

            case JoypadButtonInputBinding button:
                store.Set(section, $"{prefix}/type", "joypad_button");
                store.Set(section, $"{prefix}/button_index", button.ButtonIndex);
                break;

            case JoypadAxisInputBinding axis:
                store.Set(section, $"{prefix}/type", "joypad_axis");
                store.Set(section, $"{prefix}/axis", axis.Axis);
                store.Set(section, $"{prefix}/axis_value", axis.AxisValue);
                break;

            default:
                throw new NotSupportedException(
                    $"fxy_di_config cannot persist input binding '{binding.GetType().FullName}'.");
        }
    }

    private static void RemoveBinding(IConfigOverlayStore store, string section, string prefix)
    {
        store.Remove(section, $"{prefix}/type");
        store.Remove(section, $"{prefix}/key_code");
        store.Remove(section, $"{prefix}/button_index");
        store.Remove(section, $"{prefix}/axis");
        store.Remove(section, $"{prefix}/axis_value");
        store.Remove(section, $"{prefix}/ctrl");
        store.Remove(section, $"{prefix}/alt");
        store.Remove(section, $"{prefix}/shift");
        store.Remove(section, $"{prefix}/meta");
    }

    private static void WriteModifiers(
        IConfigOverlayStore store,
        string section,
        string prefix,
        bool ctrl,
        bool alt,
        bool shift,
        bool meta)
    {
        WriteBool(store, section, $"{prefix}/ctrl", ctrl);
        WriteBool(store, section, $"{prefix}/alt", alt);
        WriteBool(store, section, $"{prefix}/shift", shift);
        WriteBool(store, section, $"{prefix}/meta", meta);
    }

    private static void WriteBool(IConfigOverlayStore store, string section, string key, bool value)
    {
        if (value)
        {
            store.Set(section, key, true);
        }
        else
        {
            store.Remove(section, key);
        }
    }

    private static bool ReadBool(IConfigOverlayStore store, string section, string prefix, string key)
        => store.TryGet<bool>(section, $"{prefix}/{key}", out var value) && value;

    private InputBinding WithDisplayName(InputBinding binding)
        => _displayNameFormatter is null
            ? binding
            : binding switch
            {
                KeyInputBinding key => key with { DisplayName = _displayNameFormatter(binding) },
                MouseButtonInputBinding mouse => mouse with { DisplayName = _displayNameFormatter(binding) },
                JoypadButtonInputBinding button => button with { DisplayName = _displayNameFormatter(binding) },
                JoypadAxisInputBinding axis => axis with { DisplayName = _displayNameFormatter(binding) },
                _ => binding,
            };

    private static string GetBindingPrefix(string key, int index) => $"{key}/{index}";
}
