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
        _seenVersion = World.Version;
        WorldReplaced?.Invoke();
    }

    public override void _Process(double delta)
    {
        TicksThisFrame = 0;
        SimMsThisFrame = 0;
        if (!Paused)
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
                    World.Tick();
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
        CheckVersion();
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
        for (int i = 0; i < ticks; i++) World.Tick();
        Alpha = 1f;
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

    public int Erase(ReadOnlySpan<int> cells)
    {
        int n = World.Erase(cells);
        CheckVersion();
        return n;
    }
}
