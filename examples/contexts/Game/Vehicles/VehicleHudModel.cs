using Godot;

namespace ContextsExample.Game;

public sealed class VehicleHudModel
{
    private readonly VehicleController _controller;

    public VehicleHudModel(VehicleController controller)
    {
        _controller = controller;
    }

    public string BuildLine(string name)
        => $"{name}: lap {_controller.Laps} cp {_controller.Checkpoint} "
            + $"speed {Mathf.RoundToInt(_controller.Speed)} "
            + $"| {_controller.InputLabel} | {_controller.Camera.Label}/{_controller.Camera.Id}";
}
