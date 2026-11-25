using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Owns the simulation and runs it on the fixed 10 Hz tick (SimParams.TickHz) with an accumulator, like the
/// toy's frame loop: speeds 1x, 2x, 4x and 8x run more ticks, never longer ones. A per-frame time budget caps
/// the simulation work, so a slow machine slows the game down instead of stalling it (ticks the budget cuts are
/// dropped, at most one is carried over). Every edit goes through the commands here, so the view layers learn
/// about it from <see cref="Edited"/>; layers read the world through its read-only spans.
/// Runs before the view layers each frame (ProcessPriority), so they always draw this frame's state.
/// </summary>
public partial class SimDriver : Node
{
    public static readonly int[] Speeds = { 1, 2, 4, 8 };
    /// <summary>Never more ticks than this in one frame (8x at 15 fps), whatever the budget allows.</summary>
    public const int MaxTicksPerFrame = 12;
    /// <summary>Simulation work allowed per frame, in milliseconds (the toy uses 10).</summary>
    public double TickBudgetMs = 8.0;

    /// <summary>Parameters for every World made here (Main sets them before the driver enters the tree).</summary>
    public SimParams Params = SimParams.Default;
    public World World { get; private set; } = null!;
    public int Seed { get; private set; } = 1;
    public int WalkerCount { get; private set; }
    public bool Paused { get; private set; }
    /// <summary>Time held without pausing (the page turn: the new chapter's town waits for the page to open).</summary>
    public bool Held;
    public int Speed { get; private set; } = 1;

    /// <summary>Fraction of the way from the previous tick to the current one, for drawing walkers between ticks.</summary>
    public float Alpha { get; private set; } = 1f;
    /// <summary>Seconds of animation time: advances with game speed and stops while paused (walker bob, water).</summary>
    public double AnimTime { get; private set; }
    /// <summary>Smoothed simulation cost per tick, in milliseconds.</summary>
    public double MsPerTick { get; private set; }
    /// <summary>Ticks actually run per real second, smoothed (10 at 1x, 80 at 8x when the budget holds).</summary>
    public double TicksPerSecond { get; private set; }
    /// <summary>Simulation milliseconds spent this frame, and ticks run this frame.</summary>
    public double SimMsThisFrame { get; private set; }
    public int TicksThisFrame { get; private set; }
    /// <summary>Frames in which the time budget cut ticks short (since the world was made).</summary>
    public int BudgetHits { get; private set; }

    /// <summary>A new World replaced the old one (reset or new map): rebuild everything drawn from the map.</summary>
    public event Action? WorldReplaced;
    /// <summary>The world's edit version changed (fence, bridge, erase): rebuild fences and bridges.</summary>
    public event Action<int>? Edited;
    /// <summary>Pause or speed changed.</summary>
    public event Action? TimeChanged;
    /// <summary>
    /// The library built a new cottage during a tick (house growth, Chapter I): the building's index, raised once per house
    /// in build order, after the ticks and before <see cref="Edited"/>. Never allocates.
    /// </summary>
    public event Action<int>? HouseBuilt;
    /// <summary>Grown houses announced so far (World.Grown.Length once the frame's ticks are told).</summary>
    public int HousesSeen => _grownSeen;
    private int _grownSeen;
    /// <summary>
    /// A new in-game day began during a tick (raised from inside the tick loop, also during a fast-forward, right after
    /// the tick that closed the day): the ledger snapshots its running totals here, so "today" is right after a jump.
    /// Argument: the new day's index from 0. Never allocates.
    /// </summary>
    public event Action<long>? DayStarted;
    private long _day;

    private void TickOnce()
    {
        var w = World;
        // What the game can see change across the step, kept only when the step turns out slow (SlowStepMs).
        long searches = w.FieldSearches;
        int houses = w.Houses.Length, version = w.Version;
        var rv = w.River;
        bool flooded = rv != null && rv.Flooded, frozen = rv != null && rv.Frozen, raining = w.RainOn && w.Weather.Raining;
        double decayAt = Math.Floor(w.Time / w.Params.DecayStepSimSec + 1e-9);
        long t0 = Stopwatch.GetTimestamp();
        w.Tick();
        double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        long d = (long)Math.Floor(w.Time / w.Params.DaySimSec + 1e-9);
        bool dawn = d != _day;
        _stepInFrame++;
        if (ms > SlowStepMs)
        {
            SlowSteps++;
            LastSlowStep = new StepNote
            {
                Tick = w.Ticks, Day = d + 1, Ms = ms, StepInFrame = _stepInFrame, Searches = w.FieldSearches - searches,
                NewHouses = w.Houses.Length - houses, Dawn = dawn, Edited = w.Version != version,
                Decay = Math.Floor(w.Time / w.Params.DecayStepSimSec + 1e-9) != decayAt,
                FloodChanged = rv != null && rv.Flooded != flooded, FrozenChanged = rv != null && rv.Frozen != frozen,
                RainChanged = w.RainOn && w.Weather.Raining != raining,
            };
        }
        if (dawn) { _day = d; DayStarted?.Invoke(d); }
    }

    /// <summary>A single step over this many milliseconds is recorded in <see cref="LastSlowStep"/> (the perf scene prints it).</summary>
    public const double SlowStepMs = 20.0;
    /// <summary>Steps over <see cref="SlowStepMs"/> so far, and the last of them with what the game knows ran in it.</summary>
    public int SlowSteps { get; private set; }
    public StepNote LastSlowStep { get; private set; }
    private int _stepInFrame;

    /// <summary>One simulation step as the game saw it: its tick and day, its time, and what changed across it.</summary>
    public struct StepNote
    {
        public long Tick, Day, Searches;
        public double Ms;
        public int StepInFrame, NewHouses;
        public bool Dawn, Edited, Decay, FloodChanged, FrozenChanged, RainChanged;
    }

    /// <summary>What ran in a step, in words (builds a string: only for a slow step's report).</summary>
    public static string Describe(in StepNote s)
    {
        var parts = new List<string>();
        if (s.Searches > 0) parts.Add($"field refresh ({s.Searches} route searches)");
        if (s.NewHouses > 0) parts.Add(s.NewHouses == 1 ? "a house arrived" : $"{s.NewHouses} houses arrived");
        if (s.Dawn) parts.Add("a new day began");
        if (s.Decay) parts.Add("the wear decay step");
        if (s.FloodChanged) parts.Add("a flood began or ended");
        if (s.FrozenChanged) parts.Add("the river froze or thawed");
        if (s.RainChanged) parts.Add("rain began or stopped");
        if (s.Edited) parts.Add("an edit");
        return parts.Count == 0 ? "nothing the game can see (walkers and boats only)" : string.Join(", ", parts);
    }

    private double _acc;
    private int _seenVersion = -1;
    private readonly List<int> _lineScratch = new();

    public override void _EnterTree()
    {
        ProcessPriority = -100;
        if (World == null) MakeWorld(Seed, Params.DefaultWalkers);
    }

    /// <summary>Starts over with the same seed (or a given one), keeping the walker count, speed and pause.</summary>
    public void Reset(int? seed = null) => MakeWorld(seed ?? Seed, WalkerCount);

    /// <summary>A new map: the toy's next-seed rule, so the toy and the game walk through the same seeds.</summary>
    public void NewMap() => MakeWorld(NextSeed(Seed), WalkerCount);

    public static int NextSeed(int seed)
    {
        // ((imul(seed, 48271) + 12345) % 99991 + 99991) % 99991 + 1, in 32-bit integer arithmetic as in JavaScript.
        // Math.imul wraps to int32; the + 12345 that follows does not, hence the long.
        long m = ((long)unchecked(seed * 48271) + 12345) % 99991;
        return (int)((m + 99991) % 99991) + 1;
    }

    public void SetWalkers(int n)
    {
        WalkerCount = Math.Clamp(n, 0, World.Params.MaxWalkers);
        World.SetWalkerCount(WalkerCount);
    }

    public void SetPaused(bool p) { if (p != Paused) { Paused = p; TimeChanged?.Invoke(); } }
    public void TogglePause() => SetPaused(!Paused);

    public void SetSpeed(int s)
    {
        int v = Array.IndexOf(Speeds, s) >= 0 ? s : 1;
        if (v != Speed) { Speed = v; TimeChanged?.Invoke(); }
    }

    private void MakeWorld(int seed, int walkers)
    {
        Seed = seed <= 0 ? 1 : seed;
        World = new World(Seed, walkers, Params);
        WalkerCount = World.WalkerCount;
        _acc = 0;
        Alpha = 1f;
        BudgetHits = 0;
        // The lever's tick belongs to the old world: a new one counts its ticks from 0 again, so a stale value could make a
        // keeper's raise at that tick read as the player's.
        LastLeverTick = -1;
        _seenVersion = World.Version;
        _grownSeen = World.Grown.Length;
        _day = (long)Math.Floor(World.Time / World.Params.DaySimSec + 1e-9);
        WorldReplaced?.Invoke();
    }

    public override void _Process(double delta)
    {
        _stepInFrame = 0;
        TicksThisFrame = 0;
        SimMsThisFrame = 0;
        if (!Paused && !Held)
        {
            double dt = Math.Min(0.25, delta);
            AnimTime += dt * Speed;
            _acc += dt * Speed * World.Params.TickHz;
            int n = (int)Math.Floor(_acc);
            if (n > MaxTicksPerFrame) { n = MaxTicksPerFrame; _acc = n; }
            if (n > 0)
            {
                long t0 = Stopwatch.GetTimestamp();
                long limit = t0 + (long)(TickBudgetMs * Stopwatch.Frequency / 1000.0);
                int done = 0;
                while (done < n)
                {
                    TickOnce();
                    done++;
                    if (Stopwatch.GetTimestamp() > limit) break;
                }
                double spent = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (done < n) BudgetHits++;
                SimMsThisFrame = spent;
                TicksThisFrame = done;
                MsPerTick = MsPerTick == 0 ? spent / done : MsPerTick * 0.9 + spent / done * 0.1;
                _acc = Math.Min(1.0, _acc - done);
            }
            Alpha = (float)Math.Min(1.0, _acc);
        }
        else Alpha = 1f;
        if (delta > 0) TicksPerSecond = TicksPerSecond * 0.95 + TicksThisFrame / delta * 0.05;
        CheckGrowth();
        CheckVersion();
    }

    /// <summary>Raises <see cref="HouseBuilt"/> for every cottage the library added since the last call.</summary>
    public void CheckGrowth()
    {
        var grown = World.Grown;
        while (_grownSeen < grown.Length) HouseBuilt?.Invoke(grown[_grownSeen++].Building);
    }

    /// <summary>Raises <see cref="Edited"/> when the world's edit version moved (from a command here or anywhere).</summary>
    public void CheckVersion()
    {
        if (World.Version == _seenVersion) return;
        _seenVersion = World.Version;
        Edited?.Invoke(_seenVersion);
    }

    // ---------------------------------------------------------------- commands (tools and tests)

    /// <summary>Runs ticks at once, outside the frame loop (tests, fast-forward). Raises Edited if needed.</summary>
    public void StepTicks(int ticks)
    {
        for (int i = 0; i < ticks; i++) TickOnce();
        Alpha = 1f;
        CheckGrowth();
        CheckVersion();
    }

    public int PlaceFence(ReadOnlySpan<int> cells)
    {
        int n = World.PlaceFence(cells);
        CheckVersion();
        return n;
    }

    /// <summary>A fence along the 4-connected line between two cells (clipped to the map). Returns cells newly fenced.</summary>
    public int PlaceFenceLine(int x0, int y0, int x1, int y1)
    {
        _lineScratch.Clear();
        World.Grid.Line4(x0, y0, x1, y1, _lineScratch);
        return PlaceFence(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_lineScratch));
    }

    /// <summary>A footbridge across the river at row cy (cx is the clicked column). Null if none fits there.</summary>
    public Bridge? PlaceBridge(int cx, int cy)
    {
        var b = World.PlaceBridge(cx + 0.5, cy + 0.5);
        CheckVersion();
        return b;
    }

    /// <summary>Builds a ferry, drawbridge or footbridge across the river at row cy (river rules). The result says why not.</summary>
    public RiverResult PlaceCrossing(CrossingType type, int cx, int cy)
    {
        var r = World.PlaceCrossing(type, cx + 0.5, cy + 0.5);
        CheckVersion();
        return r;
    }

    /// <summary>Tick of the last hand on a drawbridge's lever (so a raise then is the player's, not the keeper's).</summary>
    public long LastLeverTick { get; private set; } = -1;

    /// <summary>Raises or lowers a drawbridge by hand.</summary>
    public RiverResult SetBridge(int id, bool raise)
    {
        LastLeverTick = World.Ticks;
        return World.SetBridge(id, raise);
    }

    public RiverResult HireKeeper(int id)
    {
        var r = World.HireKeeper(id);
        CheckVersion();
        return r;
    }

    /// <summary>Lays gravel on these cells (free in both chapters); returns how many were new.</summary>
    public int PlaceGravel(ReadOnlySpan<int> cells)
    {
        int n = World.PlaceGravel(cells);
        CheckVersion();
        return n;
    }

    /// <summary>Puts a gate in the fence at a cell (and its stair partner when it needs one); false if none fits there.</summary>
    public bool PlaceGate(int cell)
    {
        bool ok = World.PlaceGate(cell);
        CheckVersion();
        return ok;
    }

    /// <summary>Turns a gate (both cells of a two-cell gate) back into plain fence.</summary>
    public bool RemoveGate(int cell)
    {
        bool ok = World.RemoveGate(cell);
        CheckVersion();
        return ok;
    }

    public int Erase(ReadOnlySpan<int> cells)
    {
        int n = World.Erase(cells);
        CheckVersion();
        return n;
    }
}
