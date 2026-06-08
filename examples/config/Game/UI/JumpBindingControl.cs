using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class JumpBindingControl : HBoxContainer
{
    private ISettingsMonitor<InputOptions>? _input;
    private Button? _button;
    private System.IDisposable? _subscription;
    private bool _isListening;

    public override void _Ready()
    {
        _input = this.GetRequiredService<ISettingsMonitor<InputOptions>>();
        _button = GetNode<Button>("JumpButton");

        _button.Pressed += () =>
        {
            _isListening = true;
            _button.Text = "Press input...";
        };

        _subscription = _input.OnChange(_ => CallDeferred(MethodName.Refresh));
        Refresh();
    }

    public override void _ExitTree()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    public override async void _Input(InputEvent @event)
    {
        if (!_isListening || !TryCreateBinding(@event, out var binding))
        {
            return;
        }

        _isListening = false;
        GetViewport().SetInputAsHandled();
        await _input!.Set(options => options.Jump, new InputActionBindings(new[] { binding }));
    }

    private void Refresh()
    {
        if (_button is not null)
        {
            _button.Text = _input!.CurrentValue.Jump.DisplayName;
        }
    }

    private static bool TryCreateBinding(InputEvent @event, out InputBinding binding)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                binding = new KeyInputBinding(
                    (long)key.Keycode,
                    OS.GetKeycodeString(key.Keycode),
                    key.CtrlPressed,
                    key.AltPressed,
                    key.ShiftPressed,
                    key.MetaPressed);
                return true;

            case InputEventMouseButton { Pressed: true } mouse:
                binding = new MouseButtonInputBinding(
                    (long)mouse.ButtonIndex,
                    mouse.ButtonIndex.ToString(),
                    mouse.CtrlPressed,
                    mouse.AltPressed,
                    mouse.ShiftPressed,
                    mouse.MetaPressed);
                return true;

            case InputEventJoypadButton { Pressed: true } button:
                binding = new JoypadButtonInputBinding(
                    (long)button.ButtonIndex,
                    button.ButtonIndex.ToString());
                return true;

            case InputEventJoypadMotion axis when Mathf.Abs(axis.AxisValue) >= 0.5f:
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
}
