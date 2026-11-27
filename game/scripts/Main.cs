using System;
using Godot;
using TideToll.Game.Settings;
using TideToll.Sim;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Main scene: the incremental town on the right-hand page of the open book. Built in code so the scene file stays
/// tiny. Wires the driver (TownDriver: fixed-step simulation, interpolation, speed), the map layers under
/// Book/RightPage/MapOrigin (ground with the roads, plots and buildings, trees, villagers), the camera rig with the ink
/// pass, the sound and the interface. The camera starts zoomed in on the town and follows its extent as it grows,
/// until the player moves it (drag, WASD or arrows, the wheel); Home goes back to following. A level's gift makes
/// room for itself: the view follows the town again so the new piece is in sight.
/// </summary>
public partial class Main : Node3D
{
    public int StartSeed = 1;
    public TownDriver Driver { get; private set; } = null!;
    public Town Town => Driver.Town;
    public CameraRig Rig { get; private set; } = null!;
    public Node3D MapOrigin { get; private set; } = null!;
    public TownGround Ground { get; private set; } = null!;
    public TownBuildings Buildings { get; private set; } = null!;
    public TownVillagers Villagers { get; private set; } = null!;
    public RiverLayer River { get; private set; } = null!;
    public TownTouches Touches { get; private set; } = null!;
    public RiverBoats Boats { get; private set; } = null!;
    public RiverAudio RiverSound { get; private set; } = null!;
    public InkOutline Ink { get; private set; } = null!;
    public TownAudio Audio { get; private set; } = null!;
    public TownHud Hud { get; private set; } = null!;
    public TownSummary Summary { get; private set; } = null!;
    public SettingsRuntime Settings { get; private set; } = null!;
    public TownSaver Saver { get; private set; } = null!;
    public PauseMenu Menu { get; private set; } = null!;
    public BookCover Cover { get; private set; } = null!;
    public Node3D Book { get; private set; } = null!;
    /// <summary>Folder for the settings and the save: user:// in the game; each test sets a folder of its own first.</summary>
    public static string Profile = "user://";
    /// <summary>Tests own the window's size (--resolution, WindowSize): the stored window mode and size are not applied.</summary>
    public static bool KeepWindow;
    /// <summary>A note for the margin from start-up (a damaged save), shown once the interface exists.</summary>
    public string StartNote { get; private set; } = "";
    private Node3D? _trees;
    /// <summary>The camera follows the town's extent (false once the player moves the view, true again on Home or a gift).</summary>
    public bool Following => !Rig.UserMoved;
    /// <summary>The town's extent in map-local metres (x, z), as followed.</summary>
    public Rect2 Extent { get; private set; }

    private double _followClock;
    private Vector2 _limitsFor;

    /// <summary>What the interface shows for the current zoom: everything, a slim strip, or the book (summary page).</summary>
    public enum ZoomStage { Town, Page, Book }
    /// <summary>View heights where the interface thins to the slim strip and where the book counts as in view.</summary>
    public float SlimSize => Rig.MapViewSize * 0.86f;
    public float BookStageSize => (Rig.PlaySize + Rig.MaxSize) / 2f;
    public ZoomStage Stage => Rig.Camera.Size >= BookStageSize ? ZoomStage.Book : Rig.Camera.Size >= SlimSize ? ZoomStage.Page : ZoomStage.Town;

    /// <summary>The zoom's outer limit: the whole open book with a little table round it, for this window shape.</summary>
    private void SetZoomLimits()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp.Y < 1) return;
        _limitsFor = vp;
        float aspect = vp.X / vp.Y;
        Rig.MaxSize = Math.Max(BookBuilder.CoverSize.Y * 1.10f, BookBuilder.CoverSize.X * 1.06f / Math.Max(0.3f, aspect));
        // Round L3: zoomed all the way out the book stands in ONE place, the place of the settings spread (which keeps
        // its margins to the window and the navigation row): the zoom's outer end is the spread view.
        Rig.OuterCentre = null;
        // Last look: the page stage keeps the navigation row (64 at the interface's scale, and a little air) off the book.
        Rig.NavRoomPx = 72f * UiKit.ScaleFor(vp.Y);
        if (Menu != null && Menu.SpreadPagesRect.Size.Y >= 1f)
        {
            _limitsPages = Menu.SpreadPagesRect;
            var (c, size) = Menu.SpreadView();
            Rig.MaxSize = size;
            Rig.OuterCentre = c;
        }
    }

    private Rect2 _limitsPages;

    public override void _Ready()
    {
        // Settings first: the actions, the stored settings loaded and applied (sound buses, window, keys).
        Settings = new SettingsRuntime
        {
            Name = "Settings", SettingsPath = Profile + "settings.cfg", Display = KeepWindow ? new FixedWindowSeam() : null,
        };
        AddChild(Settings);
        GetTree().AutoAcceptQuit = false; // the window's close button saves first (_Notification)
        var args = OS.GetCmdlineUserArgs();
        bool seedGiven = false;
        foreach (var a in args) if (a.StartsWith("--seed=") && int.TryParse(a.Substring(7), out int sd)) { StartSeed = sd; seedGiven = true; }
        AddChild(MakeEnvironment());
        AddChild(MakeSun());
        BookBuilder.Build(this, SimParams.Default, 1);
        MapOrigin = GetNode<Node3D>("Book/RightPage/MapOrigin");
        Book = GetNode<Node3D>("Book");
        Cover = new BookCover();
        Cover.Init(Book, MapOrigin);

        Driver = new TownDriver { Name = "Driver", ProcessPriority = -100 };
        AddChild(Driver);
        Driver.Reset(StartSeed);

        Ground = new TownGround(); MapOrigin.AddChild(Ground); Ground.Init(Driver);
        Buildings = new TownBuildings(); MapOrigin.AddChild(Buildings); Buildings.Init(Driver);
        var sails = new MillSails(); sails.Init(Driver); AddChild(sails);   // night 3: every "Sails" node turns, slowly when idle
        PlantTrees();
        Driver.Replaced += PlantTrees;
        Villagers = new TownVillagers(); MapOrigin.AddChild(Villagers); Villagers.Init(Driver);
        Touches = new TownTouches(); MapOrigin.AddChild(Touches); Touches.Init(Driver);
        var life = new TownLife(); MapOrigin.AddChild(life); life.Init(Driver);   // night 3 (asset track LF): animals, birds, gardens
        River = new RiverLayer(); MapOrigin.AddChild(River); River.Init(this);
        Boats = new RiverBoats(); MapOrigin.AddChild(Boats); Boats.Init(Driver);
        RiverSound = new RiverAudio(); AddChild(RiverSound); RiverSound.Init(Driver);

        Rig = new CameraRig
        {
            Name = "CameraRig", MapOrigin = MapOrigin, MapCells = TownGround.N, CellMetres = TownGround.Cell,
            BookSize = BookBuilder.CoverSize, LeftDragPans = true, MinSize = 36f,
        };
        AddChild(Rig);
        // One continuous zoom (round 2): from close on the town (tilted) out to the whole open book (straight down).
        Rig.TiltFlatSize = Rig.MapViewSize * 0.97f;
        // Round 4: about 50 degrees at the closest view, and the tilt changes from the first wheel notch.
        Rig.TiltDegrees = 50f;
        Rig.TiltFullSize = Rig.MinSize;
        SetZoomLimits();
        Rig.HomePressed = () => { Rig.UserMoved = false; Follow(instant: false); };
        Ink = new InkOutline();
        Ink.Attach(Rig.Camera);

        Audio = new TownAudio(); AddChild(Audio); Audio.Init(Driver);
        var feel = new TownFeel(); MapOrigin.AddChild(feel); feel.Init(this);   // night 3: small rewards on the map
        Audio.SetView(() => (Rig.PivotLocal, Rig.Camera.Size, Rig.MapViewSize));
        Summary = new TownSummary(); Summary.Init(GetNode<Node3D>("Book/LeftPage"), Driver);
        Hud = new TownHud(); AddChild(Hud); Hud.Init(this);
        Rig.PressBlocked = Hud.OverInterface;
        Saver = new TownSaver(); AddChild(Saver); Saver.Init(this);
        Menu = new PauseMenu(); AddChild(Menu); Menu.Init(this);
        Follow(instant: true);
        // Continue the saved town (camera included); a damaged one is set aside and a note says so.
        if (!seedGiven && !Saver.TryContinue(out string note) && note != "") { StartNote = note; Hud.Trouble(note); }
    }

    private void PlantTrees()
    {
        _trees?.QueueFree();
        _trees = TownTrees.Make(Driver.Town.Plan);
        MapOrigin.AddChild(_trees);
    }

    /// <summary>Starts a town from a new seed at level 1 (the settings are not touched).</summary>
    public void NewTown()
    {
        int seed = Driver.Seed;
        var rng = new Random();
        while (seed == Driver.Seed) seed = rng.Next(1, int.MaxValue);
        Driver.Reset(seed);
        if (Menu.IsPaused) Menu.KeepView(Rig.PivotLocal, Rig.TargetSize, moved: false);
        else { Rig.UserMoved = false; Follow(instant: true); }
        Saver.SaveNow();
    }

    /// <summary>The camera as it should be saved: where the player left it, also while the book is closed.</summary>
    public (Vector3 pivot, float size, bool moved) ViewToKeep()
        => Menu != null && Menu.IsPaused ? Menu.Kept : (Rig.PivotLocal, Rig.TargetSize, Rig.UserMoved);

    public void RestoreView(Vector3 pivot, float size, bool moved)
    {
        if (Menu != null && Menu.IsPaused) { Menu.KeepView(pivot, size, moved); return; }
        Rig.UserMoved = moved;
        if (moved) Rig.LookAt(pivot, size, instant: true);
        else Follow(instant: true);
    }

    /// <summary>The camera for the closed book: the cover centred, with a little table round it.</summary>
    public (Vector3 centre, float size) ClosedBookView()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        float aspect = vp.X / Math.Max(1f, vp.Y);
        var sz = BookCover.ClosedSize;
        float size = Math.Max(sz.Y * 1.08f, sz.X * 1.15f / Math.Max(0.3f, aspect));
        var local = MapOrigin.GlobalTransform.AffineInverse() * (Book.GlobalTransform * BookCover.ClosedCentre);
        return (new Vector3(local.X, 0, local.Z), size);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
    }

    /// <summary>Tests set this to see the quit path without ending the run.</summary>
    public bool HoldQuit;
    public int QuitRequests { get; private set; }

    /// <summary>Quit (the menu or the window's close button): save, then quit.</summary>
    public void QuitGame()
    {
        Saver?.SaveNow();
        QuitRequests++;
        if (!HoldQuit) GetTree().Quit();
    }

    /// <summary>The bounding box of every given plot and open road, in map-local metres.</summary>
    public Rect2 TownExtent()
    {
        var t = Driver.Town;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        // Round 3: frame what is built; the next staked plot only stretches the frame a little, so it is just in view
        // without pulling the town off centre.
        foreach (var p in t.Plan.Plots)
        {
            if (!t.PlotGiven(p.Id)) continue;
            x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X + p.W); y1 = Math.Max(y1, p.Y + p.H);
        }
        for (int r = 0; r < t.RoadCount; r++)
        {
            if (!t.RoadOpen(r)) continue;
            var road = t.Plan.Roads[r];
            for (int k = 0; k < road.Xs.Length; k++)
            {
                x0 = Math.Min(x0, road.Xs[k]); y0 = Math.Min(y0, road.Ys[k]);
                x1 = Math.Max(x1, road.Xs[k] + 1); y1 = Math.Max(y1, road.Ys[k] + 1);
            }
        }
        RiverLayer.Widen(t, ref x0, ref y0, ref x1, ref y1);
        if (x0 <= x1)
        {
            const float stretch = 4f;
            float bx0 = x0, by0 = y0, bx1 = x1, by1 = y1;
            foreach (var p in t.Plan.Plots)
            {
                if (t.PlotGiven(p.Id) || p.Level != t.Level + 1) continue;
                x0 = Math.Min(x0, Math.Max(p.X, bx0 - stretch)); y0 = Math.Min(y0, Math.Max(p.Y, by0 - stretch));
                x1 = Math.Max(x1, Math.Min(p.X + p.W, bx1 + stretch)); y1 = Math.Max(y1, Math.Min(p.Y + p.H, by1 + stretch));
            }
        }
        if (x0 > x1) { x0 = t.Plan.WindowX; y0 = t.Plan.WindowY; x1 = x0 + t.Plan.WindowW; y1 = y0 + t.Plan.WindowH; }
        float c = TownGround.Cell;
        return new Rect2(x0 * c, y0 * c, (x1 - x0) * c, (y1 - y0) * c);
    }

    /// <summary>
    /// Frames the town: its extent with a little room round it, in the space the interface leaves free (the ledger and
    /// notes on the left, the bookmarks on the right). Under the tilt the ground's depth shows shortened (sin of the
    /// pitch) and buildings stand up out of it, so the height needed is solved for the pitch that the size itself gives.
    /// Never zoomed out past the slim-interface point, so the follow view always has the whole interface.
    /// </summary>
    public void Follow(bool instant)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp.Y < 1) return;
        var ext = TownExtent();
        Extent = ext;
        float s = UiKit.ScaleFor(GetWindow().Size.Y); // as the interface: built from the window's real height
        float left = 362f * s, right = 300f * s, top = 10f * s, bottom = 10f * s;
        float usableW = Math.Max(200f, vp.X - left - right), usableH = Math.Max(200f, vp.Y - top - bottom);
        float pad = 5f, rise = 7f; // metres of room round the town; height of the tallest building
        float size = Rig.MinSize;
        for (int k = 0; k < 4; k++)
        {
            float p = Mathf.DegToRad(Rig.PitchFor(size));
            float needH = (ext.Size.Y + 2 * pad) * MathF.Sin(p) + rise * MathF.Cos(p);
            size = Math.Max(needH * vp.Y / usableH, (ext.Size.X + 2 * pad) * vp.Y / usableW);
            size = Mathf.Clamp(size, Rig.MinSize + 10f, SlimSize * 0.97f);
        }
        var centre = ext.GetCenter();
        float pitch = Mathf.DegToRad(Rig.PitchFor(size));
        float shift = (left - right) / 2f * size / vp.Y;
        // Buildings rise toward the top of the screen: move the ground centre a little that way to keep them in.
        float lift = rise * MathF.Cos(pitch) / 2f / Math.Max(0.3f, MathF.Sin(pitch));
        Rig.LookAt(new Vector3(centre.X - shift, 0, centre.Y - lift * 0.5f), size, instant);
    }

    public override void _Process(double delta)
    {
        bool follow = (GameSettings.Current?.FollowTown ?? true) && !Menu.IsPaused;
        if (follow)
            foreach (var e in Driver.Events)
                if (e.Kind == TownEventKind.LevelReached && e.Level > 1) Rig.UserMoved = false; // the gift makes room
        _followClock += delta;
        if (follow && !Rig.UserMoved && _followClock >= 0.25) { _followClock = 0; Follow(instant: false); }
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp.Y < 1f || Rig.Camera.Size <= 0f) return;
        if (vp != _limitsFor || Menu.SpreadPagesRect != _limitsPages) SetZoomLimits();
        Ink.Update(Rig.Camera, vp);
        // Round 7: not while the menu has the book (the town stands still; PauseMenu writes the page before each motion).
        Summary.Watched = Rig.Camera.Size >= Rig.MapViewSize * 0.8f && !Menu.IsPaused;
        float pxPerM = vp.Y / Rig.Camera.Size * 1080f / vp.Y;
        // Villagers read as people: a little over life size close in, and kept about 15 px across as the view zooms
        // out (the people are the point of the game), up to three times life size.
        Villagers.WalkerScale = Mathf.Clamp(15f / pxPerM, 1.1f, 3f);
    }

    /// <summary>World position of a map cell centre.</summary>
    public Vector3 CellToWorld(float x, float y) => MapOrigin.GlobalTransform * new Vector3((x + 0.5f) * TownGround.Cell, 0, (y + 0.5f) * TownGround.Cell);

    private static DirectionalLight3D MakeSun() => new()
    {
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
            TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        return new WorldEnvironment { Name = "Environment", Environment = env };
    }
}

/// <summary>The settings' display seam without the window mode and size (tests keep the window they set).</summary>
public sealed class FixedWindowSeam : DisplaySeam
{
    public override void ApplyWindow(Window w, WindowMode mode, Vector2I size) { }
}
