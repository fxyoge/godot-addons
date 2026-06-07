using Godot;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class GodotInputActionBinding : IRuntimeConfigBinding<InputActionBinding>
{
    private readonly string _actionName;
    private readonly long _fallbackKeyCode;

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
                return CreateBinding((long)keyEvent.Keycode);
            }
        }

        return CreateBinding(_fallbackKeyCode);
    }

    public void Apply(InputActionBinding value)
    {
        EnsureAction();
        InputMap.ActionEraseEvents(_actionName);

        if (value.KeyCode == 0)
        {
            return;
        }

        InputMap.ActionAddEvent(_actionName, new InputEventKey
        {
            Keycode = (Key)value.KeyCode,
        });
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

    private static InputActionBinding CreateBinding(long keyCode)
        => InputActionBinding.FromKeyCode(keyCode, static value => OS.GetKeycodeString((Key)value));
}
