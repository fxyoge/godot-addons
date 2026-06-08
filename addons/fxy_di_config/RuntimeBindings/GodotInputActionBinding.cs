using System.Collections.Generic;
using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotInputActionBinding : IRuntimeConfigBinding<InputActionBindings>
{
    private readonly string _actionName;
    private readonly InputActionBindings _fallbackBindings;
    private readonly bool _preserveUnsupportedEvents;

    public GodotInputActionBinding(
        string actionName,
        InputActionBindings fallbackBindings,
        bool preserveUnsupportedEvents = true)
    {
        _actionName = actionName;
        _fallbackBindings = fallbackBindings;
        _preserveUnsupportedEvents = preserveUnsupportedEvents;
    }

    public InputActionBindings ReadDefault()
        => ReadCurrent();

    public InputActionBindings ReadCurrent()
    {
        EnsureAction();

        var bindings = new List<InputBinding>();
        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (TryCreateBinding(inputEvent, out var binding))
            {
                bindings.Add(binding);
            }
        }

        return bindings.Count == 0 ? _fallbackBindings : new InputActionBindings(bindings);
    }

    public void Apply(InputActionBindings value)
    {
        EnsureAction();

        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (IsSupportedEvent(inputEvent) || !_preserveUnsupportedEvents)
            {
                InputMap.ActionEraseEvent(_actionName, inputEvent);
            }
        }

        foreach (var binding in value.Bindings)
        {
            if (TryCreateEvent(binding, out var inputEvent))
            {
                InputMap.ActionAddEvent(_actionName, inputEvent);
            }
        }
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(InputActionBindings),
            ConfigValueSource.InputMap,
            Writable: true,
            RuntimeMutable: true,
            RequiresRestart: false,
            uiHint ?? new ConfigUiHint(key, ConfigUiControl.KeyBinding));

    private void EnsureAction()
    {
        if (!InputMap.HasAction(_actionName))
        {
            InputMap.AddAction(_actionName);
        }
    }

    private static bool TryCreateBinding(InputEvent inputEvent, out InputBinding binding)
    {
        switch (inputEvent)
        {
            case InputEventKey key:
                binding = new KeyInputBinding(
                    (long)key.Keycode,
                    OS.GetKeycodeString(key.Keycode),
                    key.CtrlPressed,
                    key.AltPressed,
                    key.ShiftPressed,
                    key.MetaPressed);
                return true;

            case InputEventMouseButton mouse:
                binding = new MouseButtonInputBinding(
                    (long)mouse.ButtonIndex,
                    mouse.ButtonIndex.ToString(),
                    mouse.CtrlPressed,
                    mouse.AltPressed,
                    mouse.ShiftPressed,
                    mouse.MetaPressed);
                return true;

            case InputEventJoypadButton button:
                binding = new JoypadButtonInputBinding(
                    (long)button.ButtonIndex,
                    button.ButtonIndex.ToString());
                return true;

            case InputEventJoypadMotion axis:
                binding = new JoypadAxisInputBinding(
                    (long)axis.Axis,
                    axis.AxisValue,
                    $"{axis.Axis} {(axis.AxisValue >= 0 ? "+" : "-")}");
                return true;

            default:
                binding = null!;
                return false;
        }
    }

    private static bool TryCreateEvent(InputBinding binding, out InputEvent inputEvent)
    {
        switch (binding)
        {
            case KeyInputBinding key:
                inputEvent = new InputEventKey
                {
                    Keycode = (Key)key.KeyCode,
                    CtrlPressed = key.Ctrl,
                    AltPressed = key.Alt,
                    ShiftPressed = key.Shift,
                    MetaPressed = key.Meta,
                };
                return true;

            case MouseButtonInputBinding mouse:
                inputEvent = new InputEventMouseButton
                {
                    ButtonIndex = (MouseButton)mouse.ButtonIndex,
                    CtrlPressed = mouse.Ctrl,
                    AltPressed = mouse.Alt,
                    ShiftPressed = mouse.Shift,
                    MetaPressed = mouse.Meta,
                };
                return true;

            case JoypadButtonInputBinding button:
                inputEvent = new InputEventJoypadButton
                {
                    ButtonIndex = (JoyButton)button.ButtonIndex,
                };
                return true;

            case JoypadAxisInputBinding axis:
                inputEvent = new InputEventJoypadMotion
                {
                    Axis = (JoyAxis)axis.Axis,
                    AxisValue = axis.AxisValue,
                };
                return true;

            default:
                inputEvent = null!;
                return false;
        }
    }

    private static bool IsSupportedEvent(InputEvent inputEvent)
        => inputEvent is InputEventKey
            or InputEventMouseButton
            or InputEventJoypadButton
            or InputEventJoypadMotion;
}
