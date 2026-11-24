using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Main scene: an open book on a table seen from above, the right-hand page holding the
/// blank map where the town will be drawn. Everything is built in code so the scene file
/// stays tiny and diffable.
/// </summary>
public partial class Main : Node3D
{
    public SimParams Params { get; } = SimParams.Default;
    public CameraRig Rig { get; private set; } = null!;
    public Node3D MapOrigin { get; private set; } = null!;

    public override void _Ready()
    {
        AddChild(MakeEnvironment());
        AddChild(MakeSun());
        BookBuilder.Build(this, Params);
        MapOrigin = GetNode<Node3D>("Book/RightPage/MapOrigin");
        Rig = new CameraRig { Name = "CameraRig" };
        AddChild(Rig);
        AddChild(new Hud { Name = "Hud" });
    }

    /// <summary>World position of a cell centre on the map page (cells are 2 m).</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        float s = (float)Params.CellMeters;
        return MapOrigin.GlobalPosition + new Vector3((x + 0.5f) * s, 0, (y + 0.5f) * s);
    }

    private static DirectionalLight3D MakeSun() => new()
    {
        // One sun fixed in the world, from the south-east, low enough for long soft shadows.
        Name = "Sun",
        RotationDegrees = new Vector3(-42, 45, 0),
        LightColor = Color.FromHtml("#FFF4E0"),
        LightEnergy = 1.15f,
        ShadowEnabled = true,
        ShadowBlur = 2.0f,
        DirectionalShadowMaxDistance = 1200f,
    };

    private static WorldEnvironment MakeEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Mud.Darkened(0.3f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Palette.Paper,
            AmbientLightEnergy = 0.75f,
            // Linear keeps the palette close to its hex values; no bloom, haze or depth of field (banned).
            TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        return new WorldEnvironment { Name = "Environment", Environment = env };
    }
}
