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
    public InkOutline Ink { get; private set; } = null!;
    public TownAudio Audio { get; private set; } = null!;
    public TownHud Hud { get; private set; } = null!;
    /// <summary>The camera follows the town's extent (false once the player moves the view, true again on Home or a gift).</summary>
    public bool Following => !Rig.UserMoved;
    /// <summary>The town's extent in map-local metres (x, z), as followed.</summary>
    public Rect2 Extent { get; private set; }

    private double _followClock;

    public override void _Ready()
    {
        GameInput.Register();
        var args = OS.GetCmdlineUserArgs();
        foreach (var a in args) if (a.StartsWith("--seed=") && int.TryParse(a.Substring(7), out int sd)) StartSeed = sd;
        AddChild(MakeEnvironment());
        AddChild(MakeSun());
        BookBuilder.Build(this, SimParams.Default, 1);
        MapOrigin = GetNode<Node3D>("Book/RightPage/MapOrigin");

        Driver = new TownDriver { Name = "Driver", ProcessPriority = -100 };
        AddChild(Driver);
        Driver.Reset(StartSeed);

        Ground = new TownGround(); MapOrigin.AddChild(Ground); Ground.Init(Driver);
        Buildings = new TownBuildings(); MapOrigin.AddChild(Buildings); Buildings.Init(Driver);
        MapOrigin.AddChild(TownTrees.Make(Driver.Town.Plan));
        Villagers = new TownVillagers(); MapOrigin.AddChild(Villagers); Villagers.Init(Driver);

        Rig = new CameraRig
        {
            Name = "CameraRig", MapOrigin = MapOrigin, MapCells = TownGround.N, CellMetres = TownGround.Cell,
            BookSize = BookBuilder.CoverSize, LeftDragPans = true, MinSize = 36f,
        };
        AddChild(Rig);
        // This round the zoom stops at the full page (the book view and the menus are next round's work).
        Rig.MaxSize = Rig.PlaySize;
        Rig.HomePressed = () => { Rig.UserMoved = false; Follow(instant: false); };
        Ink = new InkOutline();
        Ink.Attach(Rig.Camera);

        Audio = new TownAudio(); AddChild(Audio); Audio.Init(Driver);
        Hud = new TownHud(); AddChild(Hud); Hud.Init(this);
        Follow(instant: true);
    }

    /// <summary>The bounding box of every given plot and open road, in map-local metres.</summary>
    public Rect2 TownExtent()
    {
        var t = Driver.Town;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in t.Plan.Plots)
        {
            if (!t.PlotGiven(p.Id) && !(p.Level == t.Level + 1)) continue; // staked plots count: the next gift is shown
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
        if (x0 > x1) { x0 = t.Plan.WindowX; y0 = t.Plan.WindowY; x1 = x0 + t.Plan.WindowW; y1 = y0 + t.Plan.WindowH; }
        float c = TownGround.Cell;
        return new Rect2(x0 * c, y0 * c, (x1 - x0) * c, (y1 - y0) * c);
    }

    /// <summary>Frames the town: its extent with room round it, between the ledger on the left and the bookmarks on the right.</summary>
    public void Follow(bool instant)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp.Y < 1) return;
        var ext = TownExtent();
        Extent = ext;
        float s = UiKit.ScaleFor(vp.Y);
        float left = 360f * s, right = 310f * s;
        float usable = Math.Max(200f, vp.X - left - right);
        float pad = 14f;
        float size = Math.Max(ext.Size.Y + 2 * pad, (ext.Size.X + 2 * pad) * vp.Y / usable);
        size = Mathf.Clamp(size, Rig.MinSize + 10f, Rig.MaxSize);
        var centre = ext.GetCenter();
        float shift = (left - right) / 2f * size / vp.Y;
        Rig.LookAt(new Vector3(centre.X - shift, 0, centre.Y), size, instant);
    }

    public override void _Process(double delta)
    {
        foreach (var e in Driver.Events)
            if (e.Kind == TownEventKind.LevelReached && e.Level > 1) Rig.UserMoved = false; // the gift makes room
        _followClock += delta;
        if (!Rig.UserMoved && _followClock >= 0.25) { _followClock = 0; Follow(instant: false); }
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp.Y < 1f || Rig.Camera.Size <= 0f) return;
        Ink.Update(Rig.Camera, vp);
        float pxPerM = vp.Y / Rig.Camera.Size * 1080f / vp.Y;
        // Villagers stay about 8 px wide when zoomed out; never smaller than the model.
        Villagers.WalkerScale = Mathf.Clamp(8f / pxPerM, 1f, 1.8f);
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
