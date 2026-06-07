using Fxyoge.DependencyInjection;
using Godot;

namespace ContextsExample.Game;

public partial class VehicleActor : Node2D
{
    public const string SceneGroup = "race_actor";

    [Export] public int StartSlot { get; set; } = 1;

    [Export] public bool UseGarageStart { get; set; }

    [Export] public float InitialHeading { get; set; } = -0.16f;

    private VehicleController? _controller;
    private VehicleHudModel? _hud;
    private ITrackSession? _track;
    private Label? _nameLabel;
    private CameraView _view = new(Vector2.Zero, 1f);

    public VehicleState State { get; } = new();

    public VehicleController Controller => _controller
        ?? throw new System.InvalidOperationException($"{Name} has not resolved its controller yet.");

    public VehicleHudModel Hud => _hud
        ?? throw new System.InvalidOperationException($"{Name} has not resolved its HUD yet.");

    public string DisplayName => Name;

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("NameLabel");
        _nameLabel.Text = DisplayName;
        _nameLabel.TopLevel = true;

        _track = this.GetRequiredService<ITrackSession>();
        State.Position = UseGarageStart
            ? _track.Course.GaragePosition
            : _track.Course.GetStartPosition(StartSlot);
        State.Heading = InitialHeading;

        _controller = this.GetRequiredService<VehicleController>();
        _hud = this.GetRequiredService<VehicleHudModel>();
        QueueRedraw();
    }

    public void Tick(double delta)
    {
        Controller.Tick(State, delta);
        QueueRedraw();
    }

    public CameraView GetCameraView(Vector2 viewportSize, double delta = 0)
        => Controller.GetCameraView(State, viewportSize, delta);

    public void ApplyView(CameraView view, Vector2 viewportSize)
    {
        _view = view;
        Position = viewportSize * 0.5f + (State.Position - view.Center) * view.Zoom;
        Rotation = State.Heading;
        Scale = Vector2.One * view.Zoom;

        if (_nameLabel is not null)
        {
            _nameLabel.GlobalPosition = GlobalPosition + new Vector2(-30f, -54f) * view.Zoom;
            _nameLabel.Scale = Vector2.One;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var tuning = Controller.Tuning;
        var accentRadius = 34f;
        DrawCircle(Vector2.Zero, accentRadius, Controller.Camera.Accent);

        var nose = new Vector2(38f, 0f);
        var rearLeft = new Vector2(-30f, -20f);
        var rearRight = new Vector2(-30f, 20f);
        DrawColoredPolygon([nose, rearRight, rearLeft], tuning.BodyColor);
        DrawLine(new Vector2(0f, 16f), new Vector2(0f, -16f), new Color(0.06f, 0.07f, 0.08f, 0.8f), 5f);
        DrawLine(Vector2.Zero, new Vector2(48f, 0f), new Color(1f, 1f, 1f, 0.72f), 3f / _view.Zoom);

        if (!Controller.IsOnRoad)
        {
            DrawCircle(Vector2.Zero, 44f, new Color(0.45f, 0.25f, 0.08f, 0.22f));
        }

    }
}
