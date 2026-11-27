using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5, "work you can watch" (docs/plans/n5/work-build-sheet.md, section C): the places answer to the work. Owns the
/// well (<see cref="WellWorks"/>), the fields (<see cref="FieldRows"/>), the mill (<see cref="MillWorks"/>), the market
/// table (<see cref="StallTable"/>) and the cottage doors (<see cref="CottageDoors"/>). Each place keeps a small picture
/// state of its own (never saved; rebuilt from the plot's number on load) and moves it forward when a unit of work is
/// finished there (<see cref="UnitFinished"/>) and continuously while anyone works there.
/// The events come from the villagers' side once the two parts are joined (WorkSites); until then, with
/// <see cref="DetectFromSim"/> on, this node finds them itself from the simulation's views each frame: a villager that
/// was working at a workplace and is no longer working there has finished a unit. Deliveries come from the town's
/// Delivery events. Nothing is allocated per frame; every change is eased over 0.15 s or more of real time.
/// </summary>
public partial class WorkPlaces : Node3D
{
    /// <summary>The one switch: true until the villagers' side calls <see cref="UnitFinished"/>, <see cref="Delivered"/>
    /// and <see cref="GoodsTaken"/> itself; then set false so nothing is counted twice.</summary>
    public static bool DetectFromSim = true;

    /// <summary>The shortest time any drawn change of a place takes (seconds of real time).</summary>
    public const float MinEase = 0.15f;
    public const float Cell = TownGround.Cell;
    private const int MaxVillagers = 128;

    public Main Main { get; private set; } = null!;
    public TownDriver Driver { get; private set; } = null!;
    public TownBuildings Buildings { get; private set; } = null!;
    public Town Town => Driver.Town;
    public WellWorks Well { get; private set; } = null!;
    public FieldRows Fields { get; private set; } = null!;
    public MillWorks Mill { get; private set; } = null!;
    public StallTable Stall { get; private set; } = null!;
    public CottageDoors Doors { get; private set; } = null!;
    public WorkPuffs Puffs { get; private set; } = null!;

    private readonly VillagerPhase[] _phase = new VillagerPhase[MaxVillagers];
    private readonly int[] _wp = new int[MaxVillagers];
    private readonly int[] _slot = new int[MaxVillagers];
    private readonly double[] _started = new double[MaxVillagers];
    private bool _primed;
    /// <summary>False in the first frame after a town is loaded or replaced: places then appear as they stand, lived in.</summary>
    public bool Primed => _primed;

    /// <summary>Units of work finished, by workplace (tests).</summary>
    public readonly int[] Units = new int[16];
    /// <summary>Deliveries seen, by plot (tests).</summary>
    public int DeliveriesSeen { get; private set; }
    /// <summary>The frame's real seconds and whether this frame is a fresh build (a loaded town: no easing in).</summary>
    public float Dt { get; private set; }

    public void Init(Main main)
    {
        Main = main;
        Driver = main.Driver;
        Buildings = main.Buildings;
        Name = "WorkPlaces";
        Puffs = new WorkPuffs(); AddChild(Puffs);
        Well = new WellWorks(this); AddChild(Well);
        Fields = new FieldRows(this); AddChild(Fields);
        Mill = new MillWorks(this); AddChild(Mill);
        Stall = new StallTable(this); AddChild(Stall);
        Doors = new CottageDoors(this); AddChild(Doors);
        Driver.Replaced += OnReplaced;
        OnReplaced();
    }

    public override void _ExitTree() { if (Driver != null) Driver.Replaced -= OnReplaced; }

    private void OnReplaced()
    {
        _primed = false;
        for (int i = 0; i < MaxVillagers; i++) { _phase[i] = VillagerPhase.Idle; _wp[i] = -1; _slot[i] = -1; }
        Array.Clear(Units);
        Puffs.Clear();
        Well.Reset(); Fields.Reset(); Mill.Reset(); Stall.Reset(); Doors.Reset();
    }

    public override void _Process(double delta)
    {
        // the places' clocks stand while the town is paused (the menu, a test's held frame)
        Dt = Driver.Paused ? 0f : (float)Math.Min(delta, 0.1);
        if (DetectFromSim) Detect();
        Well.Step(Dt); Fields.Step(Dt); Mill.Step(Dt); Stall.Step(Dt); Doors.Step(Dt);
        _primed = true;
    }

    // ---- what the villagers' side calls (and, until joined, Detect) ----

    /// <summary>A unit of work was finished at workplace <paramref name="workplace"/> by the worker on work spot
    /// <paramref name="slot"/> (the simulation's Slot; -1 when not known).</summary>
    public void UnitFinished(int workplace, int slot)
    {
        if (workplace < 0 || workplace >= Town.WorkplaceCount) return;
        if (workplace < Units.Length) Units[workplace]++;
        var w = Town.Workplace(workplace);
        switch (w.Kind)
        {
            case PlotKind.Well: Well.UnitFinished(w.Plot, slot); break;
            case PlotKind.Field: Fields.UnitFinished(w.Plot, slot); break;
            case PlotKind.Mill: Mill.UnitFinished(w.Plot, slot); break;
        }
    }

    /// <summary>A load was delivered to <paramref name="plot"/> (the market or a cottage), coming from <paramref name="from"/>
    /// (map metres, MapOrigin's space: where the villager's arms are).</summary>
    public void Delivered(int plot, Good good, Vector3 from)
    {
        if (plot < 0 || plot >= Town.Plan.Plots.Length) return;
        DeliveriesSeen++;
        if (Town.Plan.Plots[plot].Kind == PlotKind.Market) Stall.Delivered(plot, good, from);
        else if (Town.Plan.Plots[plot].Kind == PlotKind.Cottage) Doors.CameHome(plot, good);
    }

    /// <summary>The goods standing at a workplace (pails on the rim, a stook, a flour sack on the step) were taken up by the
    /// villager leaving work spot <paramref name="slot"/>.</summary>
    public void GoodsTaken(int workplace, int slot)
    {
        if (workplace < 0 || workplace >= Town.WorkplaceCount) return;
        var w = Town.Workplace(workplace);
        switch (w.Kind)
        {
            case PlotKind.Well: Well.Taken(w.Plot); break;
            case PlotKind.Field: Fields.Taken(w.Plot, slot); break;
            case PlotKind.Mill: Mill.Taken(w.Plot); break;
        }
    }

    // ---- positions the villagers' side reads (map metres, MapOrigin's space) ----

    /// <summary>Where the worker on <paramref name="slot"/> of a field stands (just behind its row's front) and the yaw it faces.</summary>
    public Vector3 FieldWorkSpot(int plot, int slot, out float yaw) => Fields.WorkSpot(plot, slot, out yaw);
    /// <summary>Where the stook on <paramref name="slot"/>'s row stands (the sheaf is taken up there).</summary>
    public Vector3 FieldStook(int plot, int slot) => Fields.StookAt(plot, slot);
    /// <summary>Where the two full pails stand on the well's rim (index 0 or 1).</summary>
    public Vector3 WellPail(int plot, int i) => Well.PailAt(plot, i);
    /// <summary>The well's crank, where the worker on <paramref name="slot"/> stands, and the yaw it faces.</summary>
    public Vector3 WellCrank(int plot, int slot, out float yaw) => Well.CrankSpot(plot, slot, out yaw);
    /// <summary>The mill's step (in front of the door) and its door's middle.</summary>
    public Vector3 MillStep(int plot) => Mill.StepAt(plot);
    public Vector3 MillDoor(int plot) => Mill.DoorAt(plot);
    /// <summary>Where the i-th flour sack stands on the mill's step (taken up by those who leave).</summary>
    public Vector3 MillFlour(int plot, int i) => Mill.FlourAt(plot, i);
    /// <summary>The middle of the market table's front edge (where a load is set down) and its i-th place (0..7).</summary>
    public Vector3 TableEdge(int plot) => Stall.EdgeAt(plot);
    public Vector3 TablePlace(int plot, int i) => Stall.PlaceAt(plot, i);
    /// <summary>A cottage's door (its middle, on the threshold).</summary>
    public Vector3 CottageDoor(int plot) => Doors.DoorAt(plot);

    // ---- detection from the simulation (until joined) ----

    private void Detect()
    {
        var t = Town;
        int n = Math.Min(t.VillagerCount, MaxVillagers);
        double now = t.Time;
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            int wp = v.Phase == VillagerPhase.Working ? v.Workplace : -1;
            if (_primed && _phase[i] == VillagerPhase.Working && _wp[i] >= 0 && wp != _wp[i])
            {
                UnitFinished(_wp[i], _slot[i]);
                GoodsTaken(_wp[i], _slot[i]);
            }
            if (wp >= 0 && (_wp[i] != wp || _phase[i] != VillagerPhase.Working || !_primed))
            {
                _slot[i] = SlotOf(t, wp, v.X, v.Y);
                _started[i] = now;
            }
            _phase[i] = v.Phase;
            _wp[i] = wp;
        }
        if (!_primed) return;
        var ev = Driver.Events;
        for (int k = 0; k < ev.Count; k++)
        {
            var e = ev[k];
            if (e.Kind != TownEventKind.Delivery) continue;
            Delivered(e.Plot, e.Good, new Vector3((float)e.X * Cell, 1.6f, (float)e.Y * Cell));
        }
    }

    /// <summary>The work spot (the simulation's Slot) a villager standing at x, y (cells) at a workplace is on.</summary>
    private static int SlotOf(Town t, int wp, double x, double y)
    {
        var w = t.Workplace(wp);
        int best = 0;
        double bd = double.MaxValue;
        int n = Math.Max(1, w.Places);
        for (int s = 0; s < n; s++)
        {
            t.WorkSpot(w.Plot, s, out double sx, out double sy, out _);
            double d = (sx - x) * (sx - x) + (sy - y) * (sy - y);
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }

    /// <summary>Workers at a workplace now (the simulation's Busy).</summary>
    public int Busy(int plot)
    {
        var t = Town;
        for (int i = 0; i < t.WorkplaceCount; i++) { var w = t.Workplace(i); if (w.Plot == plot) return w.Open ? w.Busy : 0; }
        return 0;
    }

    public int WorkplaceOf(int plot)
    {
        var t = Town;
        for (int i = 0; i < t.WorkplaceCount; i++) if (t.Workplace(i).Plot == plot) return i;
        return -1;
    }

    /// <summary>Seconds of game time one unit of work takes at a place of this kind now (before boons): read from the
    /// simulation's rules and its Tools level, for the continuous motion while someone works.</summary>
    public double UnitSeconds(PlotKind k)
    {
        double b = k == PlotKind.Well ? TownRules.WellWork : k == PlotKind.Field ? TownRules.FieldWork : TownRules.MillWork;
        return b / Town.ToolsFactorAt(Town.ToolsLevel) / (Town.BellRinging ? TownRules.BellHurry : 1);
    }

    /// <summary>The game's speed for drawn motion that follows the work (as the sails: at most 4 times).</summary>
    public float Pace => Driver.Paused ? 0f : (float)Math.Min(Driver.Speed, 4);

    /// <summary>A plot's point in its model's frame (the building's Body: turned to the door, scaled) in map metres.</summary>
    public Vector3 OnPlot(int plot, Vector3 local)
    {
        var body = Buildings.BuiltBody(plot);
        var root = Buildings.BuiltRoot(plot);
        if (body == null || root == null)
        {
            var p = Town.Plan.Plots[plot];
            return new Vector3((p.X + p.W / 2f) * Cell, 0, (p.Y + p.H / 2f) * Cell) + local;
        }
        return root.Position + body.Transform * local;
    }

    // ---- easing ----

    /// <summary>Moves x toward target so that a change of <paramref name="range"/> takes at least <paramref name="seconds"/>,
    /// slowing as it arrives (never a jump, whatever the frame rate).</summary>
    public static float Ease(float x, float target, float dt, float seconds, float range = 1f)
    {
        float d = target - x;
        float max = range / Math.Max(MinEase, seconds) * dt;
        float step = d * Math.Min(1f, dt * 2.5f / Math.Max(MinEase, seconds) * 1.6f);
        if (MathF.Abs(step) < MathF.Min(MathF.Abs(d), max * 0.25f)) step = MathF.Sign(d) * MathF.Min(MathF.Abs(d), max * 0.25f);
        return x + Math.Clamp(step, -max, max);
    }

    /// <summary>Smooth 0..1 (ease in and out).</summary>
    public static float Smooth(float u) { u = Math.Clamp(u, 0f, 1f); return u * u * (3f - 2f * u); }
}
