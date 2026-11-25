using System;
using System.Diagnostics;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Runs the flow preview for the tools, as the toy's previewer.ts does: requests are throttled while the pointer
/// moves (a new computation starts at most every 100 ms), each computation is spread over frames with
/// FlowPredictor.Begin / Step(maxWork) in bounded slices inside a hard per-frame time budget, an unchanged request is recomputed once a second
/// (traffic drifts) or when the world's edit version moves, and the last result stays on screen until the next one
/// is ready. The predictor only reads the world, so previewing never changes a run (ToolsTest checks a wear hash).
/// Plain C#, driven by <see cref="Update"/> from the tool controller's _Process.
/// </summary>
public sealed class FlowPreviewer
{
    public const double MinGapMs = 100, RefreshMs = 1000;
    /// <summary>Preview work allowed per frame (the toy's 6 ms).</summary>
    public const double BudgetMs = 6;
    /// <summary>No preview work in a frame whose simulation ticks already took longer than this (ToolController).</summary>
    public const double SkipAboveSimMs = 4;
    /// <summary>Work units per bounded predictor step (FlowPredictor.Step(int)): about 0.1 to 0.8 ms of CPU each.</summary>
    public const int SliceWork = 1000;

    private World _world;
    private FlowPredictor _predictor;
    private WhatIf _want;
    private long _wantKey, _jobKey, _shownKey;
    private bool _hasWant, _job;
    private double _shownAt, _startedAt = -1e9;
    private int _shownVersion = -1, _shownWeather;
    private bool _enabled = true;

    public FlowPreviewer(World world)
    {
        _world = world;
        _predictor = new FlowPredictor(world);
    }

    /// <summary>The preview on screen (null when none). Replaced, never mutated, so a reference change means "redraw".</summary>
    public FlowPreview? Current { get; private set; }
    /// <summary>The readout for <see cref="Current"/> ("" when none); built once per result.</summary>
    public string Text { get; private set; } = "";
    /// <summary>Previews finished so far, and predictor steps run (tests and stats).</summary>
    public int Completed { get; private set; }
    public long Steps { get; private set; }
    /// <summary>Milliseconds spent on preview work in the last frame.</summary>
    public double LastFrameMs { get; private set; }
    /// <summary>The longest bounded predictor step seen lately, in milliseconds (decays slowly; tests and stats).</summary>
    public double StepMs { get; private set; }
    /// <summary>Managed bytes allocated inside <see cref="Update"/> so far (the predictor's result and readout).</summary>
    public long AllocBytes { get; private set; }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; if (!value) Clear(); }
    }

    /// <summary>
    /// Whether the preview on screen answers the latest request. A refresh of that same request against the same
    /// edits (the once-a-second recompute as traffic drifts) leaves it settled: what is shown still answers it.
    /// </summary>
    public bool Settled => _hasWant && _shownKey == _wantKey && Current != null
                           && (!_job || (_jobKey == _shownKey && _world.Version == _shownVersion));
    public bool Busy => _job;

    public void SetWorld(World w)
    {
        _world = w;
        _predictor = new FlowPredictor(w);
        Clear();
    }

    /// <summary>Asks for a preview of <paramref name="change"/>; <paramref name="key"/> identifies it, so repeats are free.</summary>
    public void Request(in WhatIf change, long key)
    {
        if (!_enabled) return;
        if (_hasWant && _wantKey == key) return;
        _want = change;
        _wantKey = key;
        _hasWant = true;
    }

    public void Clear()
    {
        _hasWant = false;
        _job = false;
        _shownKey = 0;
        if (Current != null) { Current = null; Text = ""; }
    }

    /// <summary>Call once a frame with the real time in milliseconds.</summary>
    public void Update(double nowMs, double budgetMs)
    {
        LastFrameMs = 0;
        if (!_enabled || !_hasWant) { _job = false; return; }
        // A flood, its mud, a freeze or a thaw changes routes without being an edit: recompute at once.
        bool stale = _wantKey != _shownKey || _world.Version != _shownVersion || nowMs - _shownAt > RefreshMs || Weather() != _shownWeather;
        if (!_job && stale && nowMs - _startedAt >= MinGapMs) Start(nowMs);
        if (!_job) return;
        if (_jobKey != _wantKey && nowMs - _startedAt >= MinGapMs) Start(nowMs); // the pointer moved on
        long t0 = Stopwatch.GetTimestamp();
        long limit = t0 + (long)(budgetMs * Stopwatch.Frequency / 1000.0);
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        long now = t0;
        // A hard cap: the clock is read before every slice, and a slice is bounded (SliceWork units, under a
        // millisecond), so a frame never spends more than the budget plus one slice here. The first slice always runs,
        // so a preview always makes progress.
        do
        {
            Steps++;
            bool more = _predictor.Step(SliceWork);
            long after = Stopwatch.GetTimestamp();
            double ms = (after - now) * 1000.0 / Stopwatch.Frequency;
            StepMs = Math.Max(ms, StepMs * 0.98);
            now = after;
            if (!more)
            {
                _job = false;
                if (_jobKey == _wantKey)
                {
                    var r = _predictor.Result!;
                    Current = r;
                    Text = FlowPredictor.Describe(r, Name);
                    _shownKey = _jobKey;
                    _shownAt = nowMs;
                    _shownVersion = r.Version;
                    _shownWeather = Weather();
                    Completed++;
                }
                break;
            }
        } while (now < limit);
        AllocBytes += GC.GetAllocatedBytesForCurrentThread() - a0;
        LastFrameMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>The river's weather as a small number (flooded, muddy, frozen), so a change makes the preview stale.</summary>
    private int Weather()
    {
        var r = _world.River;
        return r == null ? 0 : (r.Flooded ? 1 : 0) | (r.Muddy ? 2 : 0) | (r.Frozen ? 4 : 0);
    }

    private void Start(double nowMs)
    {
        _predictor.Begin(_want);
        _job = true;
        _jobKey = _wantKey;
        _startedAt = nowMs;
    }

    /// <summary>Destination names for the readout ("Blocks the Well for 3 houses").</summary>
    private string Name(int building) => _world.Buildings[building].Kind switch
    {
        BuildingKind.Well => "Well",
        BuildingKind.Field => "Field",
        BuildingKind.Market => "Market",
        BuildingKind.Landing => "Landing",
        _ => "house",
    };

    /// <summary>A request key from a kind letter and cell lists (FNV-1a; collisions only cost a missed refresh).</summary>
    public static long Key(char kind, ReadOnlySpan<int> a, ReadOnlySpan<int> b = default)
    {
        unchecked
        {
            ulong h = 1469598103934665603UL ^ kind;
            foreach (int c in a) { h ^= (uint)c; h *= 1099511628211UL; }
            h ^= 0xFFFF; h *= 1099511628211UL;
            foreach (int c in b) { h ^= (uint)c; h *= 1099511628211UL; }
            return (long)(h | 1); // never 0, which means "nothing shown"
        }
    }
}
