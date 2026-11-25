using System;
using System.Diagnostics;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless performance test, the automated stand-in for the gate "Prototype 1 runs at 60 fps with 500 walkers":
/// 500 walkers on seed 1, fast-forwarded 12 days so trails and crowds exist, then a few hundred frames at 1x and
/// at 8x. Run with --fixed-fps 60 so every frame advances 1/60 s of game time however fast the machine runs it.
/// Measures CPU time per frame for everything the game does in _Process (simulation ticks, walker buffer, wear
/// texture, camera, ink, tools and flow preview, the interface, the sound director), from a node processed first to a
/// node processed last (input handlers run outside that window); there is no rendering
/// headless, so GPU cost is not included. Also measures managed bytes allocated per frame on the main thread.
/// Fails above the budget: mean 6 ms at 1x, 10 ms at 8x, and more than 64 bytes allocated per frame. A third run at 8x
/// keeps the flow preview recomputing under a fence drag (gated on time only).
/// Prints PERF lines and PERF PASS / PERF FAIL. Pass "-- --frames=N", "-- --walkers=N" or "-- --no-hud" to change the run.
/// </summary>
public partial class Perf : Node
{
    // The third run drags a fence across the busiest trail with real mouse events, moving its end every 45 frames, so
    // the flow preview keeps recomputing (spread over frames within its 6 ms budget). Its allocation is reported, not
    // gated: each finished preview builds its result and route mesh (a few kilobytes), which is not steady state.
    // Two more runs at 8x with the river's seasons on (World.StartFlood, then World.Freeze), under the 8x budgets and the
    // allocation gate: the flood wash and mud, the closed-ford mark and tag, then the ice and its ice-wear uploads.
    private static readonly (int speed, double budgetMs, bool preview, char weather)[] Runs =
        { (1, 6.0, false, ' '), (8, 10.0, false, ' '), (8, 10.0, true, ' '), (8, 10.0, false, 'f'), (8, 10.0, false, 'i') };
    private const int Warmup = 240;
    private const double AllocBudget = 64;
    /// <summary>Preview run: no frame above this (the simulation and a preview step must never stack up).</summary>
    private const double PreviewWorstMs = 25;
    /// <summary>Preview run: managed bytes allocated per finished preview (its result, readout and route mesh).</summary>
    private const double PreviewAllocBudget = 48 * 1024; // 22 KB measured on 2026-11-25: the predictor's result and route points
    private int _completed0, _skips0, _builds0, _worstTicks;
    private double _worstMs, _worstSim, _worstPreview;
    private long _predAlloc0, _routeAlloc0;
    private int _frames = 600;
    private int _walkers = 500;
    private double _simMs;
    private bool _noHud;

    private Main _main = null!;
    private Marker _first = null!;
    private double _delta;
    private int _run = -1, _frame;
    private double[] _ms = Array.Empty<double>();
    private long _alloc0;
    private int _gc0;
    private bool _failed;

    private partial class Marker : Node
    {
        public long Stamp;
        public override void _Process(double delta) => Stamp = Stopwatch.GetTimestamp();
    }

    public override void _Ready()
    {
        try { Setup(); }
        catch (Exception e) { Crash(e); }
    }

    // Godot only logs an unhandled C# exception and carries on; the test must stop and fail instead.
    private void Crash(Exception e)
    {
        GD.PrintErr($"PERF FAIL: exception {e}");
        GD.Print("PERF FAIL");
        _run = Runs.Length;
        GetTree().Quit(1);
    }

    private void Setup()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--frames=")) _frames = int.Parse(a.Substring(9));
            if (a.StartsWith("--walkers=")) _walkers = int.Parse(a.Substring(10));
            if (a == "--no-hud") _noHud = true;
        }
        GetWindow().Size = new Vector2I(1280, 720); // headless windows are 64 x 64
        // Process order is global by priority: the marker runs before the driver (-100) and this node after everything.
        _first = new Marker { Name = "First", ProcessPriority = -100000 };
        ProcessPriority = 100000;
        AddChild(_first);
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        _main.StartChapter = 2; // the river's chapter
        _main.StartWalkers = _walkers;
        // The river rules on (as the game plays) with a purse for every crossing: a ferry north of the landing and a
        // drawbridge with its keeper south of it, so boats, queues, the raft and the leaf all run during the measure.
        var rules = TideToll.Sim.RiverRules.Default; // prices from the rules, so a retuned price never breaks the set-up
        _main.River = rules with { StartMoney = rules.FerryPrice + rules.DrawbridgePrice + rules.KeeperPrice + 290 };
        AddChild(_main);
        if (_noHud) _main.GetNode("Hud").Free(); // to see what the HUD's text costs
        _main.Sim.StepTicks(6 * _main.Params.TicksPerDay);
        string river = PlaceCrossings();
        _main.Sim.StepTicks(6 * _main.Params.TicksPerDay);
        _ms = new double[_frames];
        GD.Print($"PERF setup: seed 1, {_main.Sim.World.WalkerCount} walkers, day {_main.Sim.World.Stats().Day}, " +
                 $"{_main.Sim.World.WalkersOut()} out, {_frames} measured frames per speed after {Warmup} warm-up frames");
        GD.Print($"PERF river: {river}");
        Next();
    }

    private string PlaceCrossings()
    {
        var w = _main.Sim.World;
        var r = w.River;
        if (r == null) return "off";
        int Row(int from, int step)
        {
            for (int y = from; y > 6 && y < w.Grid.Width - 6; y += step)
                if (w.CrossingWhatIf(TideToll.Sim.CrossingType.Drawbridge, w.RiverX[y], y + 0.5) != null) return y;
            return -1;
        }
        int fr = Row(r.LandingRow - 8, -1), dr = Row(r.LandingRow + 8, +1);
        var f = _main.Sim.PlaceCrossing(TideToll.Sim.CrossingType.Ferry, (int)w.RiverX[fr], fr);
        var d = _main.Sim.PlaceCrossing(TideToll.Sim.CrossingType.Drawbridge, (int)w.RiverX[dr], dr);
        var k = d.Ok ? _main.Sim.HireKeeper(d.Id) : d;
        if (!f.Ok || !d.Ok || !k.Ok) throw new InvalidOperationException($"river crossings for the perf run: ferry {f}, drawbridge {d}, keeper {k}");
        return $"ferry at row {fr}, drawbridge with keeper at row {dr}, landing row {r.LandingRow}";
    }

    private void Next()
    {
        _run++;
        _frame = -Warmup;
        if (_run < Runs.Length) _main.Sim.SetSpeed(Runs[_run].speed);
        if (_run < Runs.Length && Runs[_run].preview) StartDrag();
        if (_run < Runs.Length) Weather(Runs[_run].weather);
    }

    /// <summary>Sets the river's season for a run: 'f' a long flood, 'i' the river frozen, ' ' as it is.</summary>
    private void Weather(char w)
    {
        var world = _main.Sim.World;
        if (world.River == null || w == ' ') return;
        if (_dragging) { EndDrag(); }
        if (w == 'f') { if (world.River.Frozen) world.Thaw(); world.StartFlood(7); } // longer than the run (800 ticks at 8x)
        if (w == 'i')
        {
            for (int i = 0; i < 8 * _main.Params.TicksPerDay && world.River.Flooded; i++) world.Tick(); // let the flood end first
            if (!world.River.Frozen) world.Freeze();
        }
        GD.Print($"PERF weather '{w}': flooded {world.River.Flooded}, frozen {world.River.Frozen}, muddy {world.River.Muddy}");
    }

    private bool _dragging;
    private int _weatherAgain;

    private void EndDrag()
    {
        var p = Cell(_bx + 6, _by);
        Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
        _dragging = false;
    }

    private Vector2 _dragA;
    private int _bx, _by;

    private void StartDrag()
    {
        var w = _main.Sim.World;
        int n = w.Grid.Width, best = 0;
        float bw = -1;
        for (int c = 0; c < w.Grid.Count; c++)
        {
            int x = c % n, y = c / n;
            if (x < 12 || y < 12 || x > n - 12 || y > n - 12 || !w.CanFence(c)) continue;
            if (w.Wear[c] > bw) { bw = w.Wear[c]; best = c; }
        }
        _bx = best % n; _by = best / n;
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, Pressed = false });
        _dragA = Cell(_bx - 6, _by);
        _dragging = true;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = _dragA, GlobalPosition = _dragA });
        Input.ParseInputEvent(new InputEventMouseButton { Position = _dragA, GlobalPosition = _dragA, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
    }

    private Vector2 Cell(int x, int y) => _main.Rig.LocalToScreen(new Vector3((x + 0.5f) * 2f, 0, (y + 0.5f) * 2f));

    public override void _Process(double delta)
    {
        if (_run >= Runs.Length) return;
        try { Frame(delta); }
        catch (Exception e) { Crash(e); }
    }

    private void Frame(double delta)
    {
        // Processed last: everything the game did this frame lies between the marker's stamp and now.
        if (_run >= Runs.Length) return;
        _delta = delta;
        if (_frame >= 0 && _frame < _frames)
        {
            _ms[_frame] = (Stopwatch.GetTimestamp() - _first.Stamp) * 1000.0 / Stopwatch.Frequency;
            if (_ms[_frame] > _worstMs)
            {
                _worstMs = _ms[_frame];
                _worstSim = _main.Sim.SimMsThisFrame;
                _worstTicks = _main.Sim.TicksThisFrame;
                _worstPreview = _main.Tools.Previewer.LastFrameMs;
            }
            _simMs += _main.Sim.SimMsThisFrame;
        }
        // The seasons run on: spring thaws the ice and a flood ends; put them back so the whole run is measured in them.
        if (Runs[_run].weather == 'i' && _main.Sim.World.River is { Frozen: false }) { _main.Sim.World.Freeze(); _weatherAgain++; }
        if (Runs[_run].weather == 'f' && _main.Sim.World.River is { Flooded: false }) { _main.Sim.World.StartFlood(7); _weatherAgain++; }
        if (Runs[_run].preview && _frame % 45 == 0)
        {
            var p = Cell(_bx + 6, _by + (_frame / 45 % 5) - 2);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left });
        }
        _frame++;
        if (_frame == 0)
        {
            _simMs = 0; _alloc0 = GC.GetAllocatedBytesForCurrentThread(); _gc0 = GC.CollectionCount(0);
            _worstMs = 0; _worstSim = 0; _worstPreview = 0; _worstTicks = 0;
            var t = _main.Tools;
            _completed0 = t.Previewer.Completed; _skips0 = t.PreviewSkips; _builds0 = t.Routes.Builds;
            _predAlloc0 = t.Previewer.AllocBytes; _routeAlloc0 = t.Routes.BuildBytes;
        }
        if (_frame <= _frames) return;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - _alloc0;
        int gcs = GC.CollectionCount(0) - _gc0;
        Report(bytes, gcs);
        Next();
        if (_run >= Runs.Length)
        {
            GD.Print(_failed ? "PERF FAIL" : "PERF PASS");
            GetTree().Quit(_failed ? 1 : 0);
        }
    }

    private void Report(long bytes, int gcs)
    {
        var (speed, budget, preview, weather) = Runs[_run];
        var sorted = (double[])_ms.Clone();
        Array.Sort(sorted);
        double sum = 0;
        foreach (double v in _ms) sum += v;
        double mean = sum / _frames, p95 = sorted[(int)(_frames * 0.95)], worst = sorted[^1];
        double perFrame = bytes / (double)_frames;
        var s = _main.Sim;
        bool ok = mean <= budget && (preview || perFrame <= AllocBudget);
        _failed |= !ok;
        string what = preview ? $"{speed}x+preview" : weather == 'f' ? $"{speed}x+flood" : weather == 'i' ? $"{speed}x+ice" : $"{speed}x";
        if (preview)
        {
            // The preview run's own gates: the worst frame, and bytes per finished preview (so they cannot grow unseen).
            var t = _main.Tools;
            int done = t.Previewer.Completed - _completed0;
            double perPreview = done > 0 ? bytes / (double)done : double.PositiveInfinity;
            double pred = (t.Previewer.AllocBytes - _predAlloc0) / Math.Max(1.0, done);
            double route = (t.Routes.BuildBytes - _routeAlloc0) / Math.Max(1.0, t.Routes.Builds - _builds0);
            bool worstOk = worst < PreviewWorstMs, allocOk = perPreview <= PreviewAllocBudget;
            GD.Print($"PERF preview: {done} previews finished in the measured frames, {t.Previewer.Steps} steps, " +
                     $"{t.PreviewSkips - _skips0} frames skipped (sim over {FlowPreviewer.SkipAboveSimMs} ms), '{t.Previewer.Text}'");
            GD.Print($"PERF preview gates: worst frame {worst:F3} ms (gate < {PreviewWorstMs}) {(worstOk ? "ok" : "OVER")}; " +
                     $"(sim {_worstSim:F2} ms in {_worstTicks} ticks, preview {_worstPreview:F2} ms, longest preview step {t.Previewer.StepMs:F2} ms); " +
                     $"{perPreview / 1024:F1} KB allocated per finished preview (gate {PreviewAllocBudget / 1024:F0} KB) {(allocOk ? "ok" : "OVER")}: " +
                     $"predictor {pred / 1024:F1} KB, route mesh {route / 1024:F1} KB per build (peak {t.Routes.PeakVertices} vertices of {RouteLayer.ReserveVertices} reserved), the rest ghost, requests and readout");
            ok &= worstOk && allocOk;
            if (done < 3) { ok = false; GD.Print("PERF preview did not run"); }
        }
        _failed |= !ok;
        GD.Print($"PERF {_walkers} walkers {what}: cpu ms/frame mean {mean:F3} p95 {p95:F3} worst {worst:F3} (budget mean {budget}; of which sim {_simMs / _frames:F3}) | " +
                 $"alloc {perFrame:F1} B/frame, gen0 GCs {gcs} | sim {s.MsPerTick:F3} ms/tick, budget hits {s.BudgetHits}, " +
                 $"walkers drawn {_main.Walkers.Drawn}, wear uploads {_main.Ground.Uploads}, frame delta {_delta * 1000:F2} ms | {(ok ? "ok" : "OVER BUDGET")}");
        if (_main.Sim.World.River is { } r)
            GD.Print($"PERF river {what}: boats drawn {_main.Boats.Drawn}, rings {_main.Boats.Rings}, crossings drawn {_main.Crossings.Count}, " +
                     $"events {_main.Feed.Drained}, served {r.Stats.Served[0] + r.Stats.Served[1]}, turned back {r.Stats.TurnedBack[0] + r.Stats.TurnedBack[1]}, " +
                     $"coin {r.Money:0}c, sound cues bridge {_main.Audio.BridgeCues} calls {_main.Audio.Calls} coins {_main.Audio.CoinCues} oars {_main.Audio.OarCues} | " +
                     $"flooded {r.Flooded} (wash {_main.Ground.Flood:0.00}, ford bar {_main.Crossings.FordClosedShown}), frozen {r.Frozen} (ice {_main.Ground.Ice:0.00}, " +
                     $"ice uploads {_main.Ground.IceUploads}, ice crossings {r.Stats.IceCrossings}), weather restarted {_weatherAgain} times so far");
    }
}
