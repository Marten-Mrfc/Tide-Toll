using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Main scene: an open book on a table seen from above, the town living on the right-hand page.
/// Everything is built in code so the scene file stays tiny and diffable. This node wires the pieces:
/// the simulation driver, the map layers under MapOrigin (ground, town, scenery, fences and bridges, walkers),
/// the camera rig with the ink pass, and the HUD. Tools and interface code reach everything through the
/// properties here (see game/README.md, "Hooks"); the play layer is Tools (tools, ghosts, flow preview), Audio and
/// Ui (the interface in the book).
/// </summary>
public partial class Main : Node3D
{
    public SimParams Params { get; } = SimParams.Default;
    public SimDriver Sim { get; private set; } = null!;
    public CameraRig Rig { get; private set; } = null!;
    public Node3D MapOrigin { get; private set; } = null!;
    public MapGround Ground { get; private set; } = null!;
    public TownLayer Town { get; private set; } = null!;
    public Scenery Scenery { get; private set; } = null!;
    public EditLayer Edits { get; private set; } = null!;
    public WalkerLayer Walkers { get; private set; } = null!;
    public InkOutline Ink { get; private set; } = null!;
    public ToolController Tools { get; private set; } = null!;
    public AudioDirector Audio { get; private set; } = null!;
    public GameUi Ui { get; private set; } = null!;

    /// <summary>The two pages in world space (x, z rectangles on y = 0), for laying the interface over them.</summary>
    public Rect2 LeftPageRect => new(-(BookBuilder.PageWidth + 1f), -BookBuilder.PageDepth / 2f, BookBuilder.PageWidth, BookBuilder.PageDepth);
    public Rect2 RightPageRect => new(1f, -BookBuilder.PageDepth / 2f, BookBuilder.PageWidth + BookBuilder.StackSteps * 1.3f, BookBuilder.PageDepth);

    /// <summary>Seed and walker count to start with (tests set these before adding the scene to the tree).</summary>
    public int StartSeed = 1;
    public int StartWalkers = -1;

    public override void _Ready()
    {
        AddChild(MakeEnvironment());
        AddChild(MakeSun());
        BookBuilder.Build(this, Params);
        MapOrigin = GetNode<Node3D>("Book/RightPage/MapOrigin");

        Sim = new SimDriver { Name = "Sim" };
        AddChild(Sim);
        Sim.Reset(StartSeed);
        if (StartWalkers >= 0) Sim.SetWalkers(StartWalkers);

        Ground = new MapGround(); MapOrigin.AddChild(Ground); Ground.Init(Sim);
        Town = new TownLayer(); MapOrigin.AddChild(Town); Town.Init(Sim);
        Scenery = new Scenery(); MapOrigin.AddChild(Scenery); Scenery.Init(Sim);
        Edits = new EditLayer(); MapOrigin.AddChild(Edits); Edits.Init(Sim);
        Walkers = new WalkerLayer(); MapOrigin.AddChild(Walkers); Walkers.Init(Sim);

        Rig = new CameraRig
        {
            Name = "CameraRig",
            MapOrigin = MapOrigin,
            MapCells = Params.GridWidth,
            CellMetres = (float)Params.CellMeters,
            BookSize = BookBuilder.CoverSize,
        };
        AddChild(Rig);
        Ink = new InkOutline();
        Ink.Attach(Rig.Camera);

        Tools = new ToolController();
        AddChild(Tools);
        Tools.Init(this);
        Audio = new AudioDirector();
        AddChild(Audio);
        Audio.Init(this);
        Ui = new GameUi();
        AddChild(Ui);              // sets Rig.PlayRoomPx for the tabs, so frame afterwards
        Rig.FramePlay(instant: true);
    }

    public override void _Process(double delta)
    {
        // Zoom-dependent drawing: ink width, walker size and walker ink follow the screen pixels per metre.
        var vp = GetViewport().GetVisibleRect().Size;
        Ink.Update(Rig.Camera, vp);
        float pxPerM = vp.Y / Rig.Camera.Size * 1080f / vp.Y; // as if at 1080p, so the look is resolution-independent
        // Villagers stay about 7 px wide when zoomed out (the mesh is 1 m wide, already 1.5x life size).
        Walkers.WalkerScale = Mathf.Clamp(7f / pxPerM, 1f, 1.8f);
        Walkers.SetMetresPerPixel(Rig.Camera.Size / vp.Y);
    }

    /// <summary>World position of a cell centre on the map page (cells are 2 m).</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        float s = (float)Params.CellMeters;
        return MapOrigin.GlobalTransform * new Vector3((x + 0.5f) * s, 0, (y + 0.5f) * s);
    }

    private static DirectionalLight3D MakeSun() => new()
    {
        // One sun fixed in the world, from the south-east, low enough for long soft shadows.
        Name = "Sun",
        RotationDegrees = new Vector3(-48, 45, 0),
        LightColor = Color.FromHtml("#FFF4E0"),
        LightEnergy = 0.9f,
        ShadowEnabled = true,
        ShadowBlur = 1.5f,
        DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
        DirectionalShadowMaxDistance = 700f,
    };

    private static WorldEnvironment MakeEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Mud.Darkened(0.3f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Palette.Paper,
            AmbientLightEnergy = 0.4f,
            // Linear keeps the palette close to its hex values; no bloom, haze or depth of field (banned).
            TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        return new WorldEnvironment { Name = "Environment", Environment = env };
    }
}
