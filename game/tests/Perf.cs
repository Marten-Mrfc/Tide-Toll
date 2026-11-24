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
    private static readonly (int speed, double budgetMs, bool preview)[] Runs = { (1, 6.0, false), (8, 10.0, false), (8, 10.0, true) };
    private const int Warmup = 240;
    private const double AllocBudget = 64;
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
        _main.StartWalkers = _walkers;
        AddChild(_main);
        if (_noHud) _main.GetNode("Hud").Free(); // to see what the HUD's text costs
        _main.Sim.StepTicks(12 * _main.Params.TicksPerDay);
        _ms = new double[_frames];
        GD.Print($"PERF setup: seed 1, {_main.Sim.World.WalkerCount} walkers, day {_main.Sim.World.Stats().Day}, " +
                 $"{_main.Sim.World.WalkersOut()} out, {_frames} measured frames per speed after {Warmup} warm-up frames");
        Next();
    }

    private void Next()
    {
        _run++;
        _frame = -Warmup;
        if (_run < Runs.Length) _main.Sim.SetSpeed(Runs[_run].speed);
        if (_run < Runs.Length && Runs[_run].preview) StartDrag();
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
        Input.ParseInputEvent(new InputEventMouseMotion { Position = _dragA, GlobalPosition = _dragA });
        Input.ParseInputEvent(new InputEventMouseButton { Position = _dragA, GlobalPosition = _dragA, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
    }

    private Vector2 Cell(int x, int y) => _main.Rig.LocalToScreen(new Vector3((x + 0.5f) * 2f, 0, (y + 0.5f) * 2f));

    public override void _Process(double delta)
    {
        // Processed last: everything the game did this frame lies between the marker's stamp and now.
        if (_run >= Runs.Length) return;
        _delta = delta;
        if (_frame >= 0 && _frame < _frames)
        {
            _ms[_frame] = (Stopwatch.GetTimestamp() - _first.Stamp) * 1000.0 / Stopwatch.Frequency;
            _simMs += _main.Sim.SimMsThisFrame;
        }
        if (Runs[_run].preview && _frame % 45 == 0)
        {
            var p = Cell(_bx + 6, _by + (_frame / 45 % 5) - 2);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left });
        }
        _frame++;
        if (_frame == 0) { _simMs = 0; _alloc0 = GC.GetAllocatedBytesForCurrentThread(); _gc0 = GC.CollectionCount(0); }
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
        var (speed, budget, preview) = Runs[_run];
        var sorted = (double[])_ms.Clone();
        Array.Sort(sorted);
        double sum = 0;
        foreach (double v in _ms) sum += v;
        double mean = sum / _frames, p95 = sorted[(int)(_frames * 0.95)], worst = sorted[^1];
        double perFrame = bytes / (double)_frames;
        var s = _main.Sim;
        bool ok = mean <= budget && (preview || perFrame <= AllocBudget);
        _failed |= !ok;
        string what = preview ? $"{speed}x+preview" : $"{speed}x";
        if (preview) GD.Print($"PERF preview: {_main.Tools.Previewer.Completed} previews finished, {_main.Tools.Previewer.Steps} steps, '{_main.Tools.Previewer.Text}'");
        if (preview && _main.Tools.Previewer.Completed < 3) { ok = false; GD.Print("PERF preview did not run"); }
        _failed |= !ok;
        GD.Print($"PERF {_walkers} walkers {what}: cpu ms/frame mean {mean:F3} p95 {p95:F3} worst {worst:F3} (budget mean {budget}; of which sim {_simMs / _frames:F3}) | " +
                 $"alloc {perFrame:F1} B/frame, gen0 GCs {gcs} | sim {s.MsPerTick:F3} ms/tick, budget hits {s.BudgetHits}, " +
                 $"walkers drawn {_main.Walkers.Drawn}, wear uploads {_main.Ground.Uploads}, frame delta {_delta * 1000:F2} ms | {(ok ? "ok" : "OVER BUDGET")}");
    }
}
