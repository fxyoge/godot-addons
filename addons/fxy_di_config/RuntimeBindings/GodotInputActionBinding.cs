using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotInputActionBinding : IRuntimeConfigBinding<InputActionBinding>
{
    private readonly string _actionName;
    private readonly long _fallbackKeyCode;
    private long? _managedKeyCode;

    public GodotInputActionBinding(string actionName, long fallbackKeyCode)
    {
        _actionName = actionName;
        _fallbackKeyCode = fallbackKeyCode;
    }

    public InputActionBinding ReadDefault()
    {
        EnsureAction();

        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                _managedKeyCode = (long)keyEvent.Keycode;
                return CreateBinding(_managedKeyCode.Value);
            }
        }

        _managedKeyCode = _fallbackKeyCode;
        return CreateBinding(_fallbackKeyCode);
    }

    public void Apply(InputActionBinding value)
    {
        EnsureAction();
        RemoveManagedKeyEvent();

        if (value.KeyCode == 0)
        {
            _managedKeyCode = null;
            return;
        }

        if (HasKeyEvent(value.KeyCode))
        {
            _managedKeyCode = value.KeyCode;
            return;
        }

        InputMap.ActionAddEvent(_actionName, new InputEventKey
        {
            Keycode = (Key)value.KeyCode,
        });
        _managedKeyCode = value.KeyCode;
    }

    public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
        => new(
            section,
            key,
            typeof(InputActionBinding),
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

    private bool HasKeyEvent(long keyCode)
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (inputEvent is InputEventKey keyEvent && (long)keyEvent.Keycode == keyCode)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveManagedKeyEvent()
    {
        if (_managedKeyCode is not { } managedKeyCode)
        {
            return;
        }

        foreach (var inputEvent in InputMap.ActionGetEvents(_actionName))
        {
            if (inputEvent is InputEventKey keyEvent && (long)keyEvent.Keycode == managedKeyCode)
            {
                InputMap.ActionEraseEvent(_actionName, inputEvent);
                return;
            }
        }
    }

    private static InputActionBinding CreateBinding(long keyCode)
        => InputActionBinding.FromKeyCode(keyCode, static value => OS.GetKeycodeString((Key)value));
}
