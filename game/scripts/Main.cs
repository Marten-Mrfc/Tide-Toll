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
    /// <summary>
    /// The simulation's parameters for the chapter being played: Chapter I ("The Hamlet") is the library's Default with
    /// the river rules off (rain, mud and growing houses on); Chapter II ("The River") is the same with <see cref="River"/>
    /// on (the library then turns rain and growth off).
    /// </summary>
    public SimParams Params { get; private set; } = SimParams.Default;
    /// <summary>River rules for Chapter II (tests may change them, e.g. the starting coin, before adding the scene).</summary>
    public RiverRules? River = RiverRules.Default;
    /// <summary>The chapter to open on (1 or 2). "-- --chapter 2" on the command line opens Chapter II; "-- --no-river" is Chapter I.</summary>
    public int StartChapter = 1;
    /// <summary>The chapter being played: 1, The Hamlet (paths only), or 2, The River (boats, coin and the crossings).</summary>
    public int Chapter { get; private set; } = 1;
    /// <summary>Times the book was built for a chapter (1 at start, one more for every page turn).</summary>
    public int ChapterBuilds { get; private set; }
    /// <summary>The page-turn moment (an overlay kept across chapters).</summary>
    public PageTurn Turn { get; private set; } = null!;
    /// <summary>A new chapter's book is built and ready (tests, sound).</summary>
    public event System.Action<int>? ChapterChanged;
    public static readonly string[] ChapterNames = { "", "The Hamlet", "The River" };
    public static readonly string[] ChapterNumerals = { "", "I", "II" };
    public SimDriver Sim { get; private set; } = null!;
    public CameraRig Rig { get; private set; } = null!;
    public Node3D MapOrigin { get; private set; } = null!;
    public MapGround Ground { get; private set; } = null!;
    public TownLayer Town { get; private set; } = null!;
    public GroundMarks Marks { get; private set; } = null!;
    public Scenery Scenery { get; private set; } = null!;
    public EditLayer Edits { get; private set; } = null!;
    public WalkerLayer Walkers { get; private set; } = null!;
    public InkOutline Ink { get; private set; } = null!;
    public ToolController Tools { get; private set; } = null!;
    public AudioDirector Audio { get; private set; } = null!;
    public GameUi Ui { get; private set; } = null!;
    /// <summary>The river: boats, the landing and the wakes and marks on the water (null pieces when the rules are off).</summary>
    public RiverLayer Boats { get; private set; } = null!;
    /// <summary>Ferries, drawbridges and keepers' huts, animated from the simulation's state.</summary>
    public CrossingLayer Crossings { get; private set; } = null!;
    /// <summary>River events drained once a frame and handed to the chronicle, the sound and the marks.</summary>
    public RiverFeed Feed { get; private set; } = null!;

    /// <summary>The two pages in world space (x, z rectangles on y = 0), for laying the interface over them.</summary>
    public Rect2 LeftPageRect => new(-(BookBuilder.PageWidth + 1f), -BookBuilder.PageDepth / 2f, BookBuilder.PageWidth, BookBuilder.PageDepth);
    public Rect2 RightPageRect => new(1f, -BookBuilder.PageDepth / 2f, BookBuilder.PageWidth + BookBuilder.StackSteps * 1.3f, BookBuilder.PageDepth);

    /// <summary>Seed and walker count to start with (tests set these before adding the scene to the tree).</summary>
    public int StartSeed = 1;
    public int StartWalkers = -1;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        int at = System.Array.IndexOf(args, "--chapter");
        if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int ch)) StartChapter = ch;
        foreach (var a in args) if (a.StartsWith("--chapter=") && int.TryParse(a.Substring(10), out int ch2)) StartChapter = ch2;
        if (System.Array.IndexOf(args, "--no-river") >= 0) StartChapter = 1;
        Chapter = StartChapter == 2 ? 2 : 1;
        Turn = new PageTurn { Name = "PageTurn" };
        AddChild(Turn);
        Turn.Init(this);
        Build(StartSeed, StartWalkers, null);
    }

    /// <summary>Turns the page to the other chapter, with the page-turn moment (the C key and the chapter bookmark).</summary>
    public void TurnPage() => TurnTo(Chapter == 1 ? 2 : 1);

    /// <summary>Turns to a chapter with the page-turn moment; the new book is built while the turning leaf covers it.</summary>
    public void TurnTo(int chapter)
    {
        chapter = chapter == 2 ? 2 : 1;
        if (chapter == Chapter || Turn.Busy) return;
        Turn.Start(chapter);
    }

    /// <summary>
    /// Builds the book again for a chapter at once (the page turn calls this while its leaf covers the book; tests may
    /// call it directly): a fresh town from the current seed, with the walker count, speed, pause, sound, flow preview,
    /// wear overlay and view kept.
    /// </summary>
    public void SwitchChapter(int chapter)
    {
        chapter = chapter == 2 ? 2 : 1;
        var keep = new Kept
        {
            Seed = Sim.Seed, Walkers = Sim.WalkerCount, Speed = Sim.Speed, Paused = Sim.Paused, Sound = Audio.Enabled,
            Volume = Audio.Volume, Preview = Tools.PreviewEnabled, Overlay = Ui.Overlay, PlayView = Rig.InPlayView,
        };
        foreach (var c in GetChildren())
        {
            if (c == Turn) continue;
            RemoveChild(c);
            c.QueueFree();
        }
        Chapter = chapter;
        Build(keep.Seed, keep.Walkers, keep);
    }

    private struct Kept
    {
        public int Seed, Walkers, Speed;
        public bool Paused, Sound, Preview, Overlay, PlayView;
        public float Volume;
    }

    private void Build(int seed, int walkers, Kept? keep)
    {
        Params = SimParams.Default with { River = Chapter == 2 ? River ?? RiverRules.Default : null };
        AddChild(MakeEnvironment());
        AddChild(MakeSun());
        BookBuilder.Build(this, Params, Chapter);
        MapOrigin = GetNode<Node3D>("Book/RightPage/MapOrigin");

        Sim = new SimDriver { Name = "Sim", Params = Params };
        AddChild(Sim);
        Sim.Reset(seed);
        if (walkers >= 0) Sim.SetWalkers(walkers);

        Ground = new MapGround(); MapOrigin.AddChild(Ground); Ground.Init(Sim);
        Marks = new GroundMarks(); MapOrigin.AddChild(Marks); Marks.Init(Sim);
        Town = new TownLayer(); MapOrigin.AddChild(Town); Town.Init(Sim);
        Scenery = new Scenery(); MapOrigin.AddChild(Scenery); Scenery.Init(Sim);
        Edits = new EditLayer(); MapOrigin.AddChild(Edits); Edits.Init(Sim);
        Crossings = new CrossingLayer(); MapOrigin.AddChild(Crossings); Crossings.Init(Sim);
        Boats = new RiverLayer(); MapOrigin.AddChild(Boats); Boats.Init(Sim);
        Walkers = new WalkerLayer(); MapOrigin.AddChild(Walkers); Walkers.Init(Sim, Crossings);

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

        Feed = new RiverFeed();
        AddChild(Feed);
        Feed.Init(this);
        Tools = new ToolController();
        AddChild(Tools);
        Tools.Init(this);
        Audio = new AudioDirector();
        AddChild(Audio);
        Audio.Init(this);
        if (keep is { } k)
        {
            Sim.SetSpeed(k.Speed);
            Sim.SetPaused(k.Paused);
            Audio.SetEnabled(k.Sound);
            Audio.SetVolume(k.Volume);
            Tools.PreviewEnabled = k.Preview;
        }
        Ui = new GameUi();
        AddChild(Ui);              // sets Rig.PlayRoomPx for the tabs, so frame afterwards
        if (keep is { Overlay: true }) Ui.SetOverlay(true);
        if (keep is { PlayView: false }) Rig.FrameBook(instant: true); else Rig.FramePlay(instant: true);
        MoveChild(Turn, GetChildCount() - 1);
        ChapterBuilds++;
        ChapterChanged?.Invoke(Chapter);
    }

    public override void _Process(double delta)
    {
        // Zoom-dependent drawing: ink width, walker size and walker ink follow the screen pixels per metre.
        var vp = GetViewport().GetVisibleRect().Size;
        // A minimised or zero-height window: nothing is drawn, and every width below would divide by zero.
        if (vp.Y < 1f || Rig.Camera.Size <= 0f) return;
        Ink.Update(Rig.Camera, vp);
        float pxPerM = vp.Y / Rig.Camera.Size * 1080f / vp.Y; // as if at 1080p, so the look is resolution-independent
        // Villagers stay about 7 px wide when zoomed out (the mesh is 1 m wide, already 1.5x life size).
        Walkers.WalkerScale = Mathf.Clamp(7f / pxPerM, 1f, 1.8f);
        Walkers.SetMetresPerPixel(Rig.Camera.Size / vp.Y);
        Boats.SetMetresPerPixel(Rig.Camera.Size / vp.Y);
        Crossings.SetMetresPerPixel(Rig.Camera.Size / vp.Y);
        // Boats and the landing grow up to 2.2x when zoomed out (as villagers do), so at the play view a barge reads
        // apart from a rowboat and a waiting boat from one under way; the crossings get their map marks there.
        Boats.BoatScale = Mathf.Clamp(8f / pxPerM, 1f, 2.2f);
        Crossings.Overview = Mathf.Clamp((Rig.Camera.Size - 110f) / 60f, 0f, 1f);
        Marks.SetMetresPerPixel(Rig.Camera.Size / vp.Y);
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
