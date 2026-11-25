using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the tools through real input events (mouse motion, press and release at screen positions, keys),
/// checking the simulation's state afterwards:
/// - a fence drag fences exactly the 4-connected line between the press and release cells, with a ghost during the drag;
/// - the footbridge tool shows the bridge on hover, builds it on press and release, and gives a reason in words where
///   none fits;
/// - erase removes fences by click and by drag, and a whole bridge by a click on it;
/// - a press on a bookmark tab over the map places nothing, a drag released over a tab is cancelled, a right click
///   and Escape put the tool down;
/// - preview isolation: the same seed run with and without flow-preview activity (a fence drag and footbridge hover
///   recomputing previews between ticks) ends with the same wear grid, hollow flags and random stream.
/// Prints TOOLS PASS or TOOLS FAIL and quits with 0 or 1.
/// </summary>
public partial class ToolsTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            await TestInput.Frames(this, 3);
            var sim = _main.Sim;
            sim.SetPaused(true);              // tools work while paused, and nothing moves under the pointer
            sim.StepTicks(8 * _main.Params.TicksPerDay);
            _main.Rig.FramePlay(instant: true);
            await TestInput.Frames(this, 2);
            await Fence();
            await Bridge();
            await Erase();
            await Gravel();
            await Gate();
            await Toolbar();
            await Isolation();
            GD.Print("TOOLS PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("TOOLS FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private World W => _main.Sim.World;
    private int N => W.Grid.Width;

    private int FenceCount()
    {
        int n = 0;
        foreach (byte f in W.Fence) n += f;
        return n;
    }

    /// <summary>A horizontal run of fenceable cells on a row, found from the map so it holds on any seed.</summary>
    private (int x0, int y) FreeRun(int row, int len)
    {
        for (int y = row; y < N - 2; y++)
            for (int x = 4; x < N - len - 4; x++)
            {
                bool ok = true;
                for (int k = 0; k < len && ok; k++) ok = W.CanFence(y * N + x + k) && W.Fence[y * N + x + k] == 0;
                if (ok) return (x, y);
            }
        throw new InvalidOperationException("no room for a test fence");
    }

    private async Task Drag(Vector2 a, Vector2 b, int steps = 6)
    {
        TestInput.Motion(a);
        await TestInput.Frames(this, 1);
        TestInput.Button(a, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        for (int i = 1; i <= steps; i++) { TestInput.Motion(a.Lerp(b, i / (float)steps)); await TestInput.Frames(this, 1); }
    }

    private async Task Fence()
    {
        TestInput.Key(Key.F);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Fence, "F did not pick up the fence tool");
        // A diagonal drag: the fence must be exactly the simulation's 4-connected line between the two cells.
        var (x0, y0) = FreeRun(96, 12);
        int x1 = x0 + 7, y1 = y0 + 3;
        var expected = new List<int>();
        W.Grid.Line4(x0, y0, x1, y1, expected);
        foreach (int c in expected) Check(W.CanFence(c), "test line crosses an unfenceable cell");
        int before = FenceCount(), places = _main.Audio.Places;
        await Drag(TestInput.CellScreen(_main, x0, y0), TestInput.CellScreen(_main, x1, y1));
        Check(_main.Tools.Ghost.Pieces == expected.Count, $"ghost shows {_main.Tools.Ghost.Pieces} cells during the drag, expected {expected.Count}");
        Check(FenceCount() == before, "fence placed before release");
        TestInput.Button(TestInput.CellScreen(_main, x1, y1), MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(FenceCount() == before + expected.Count, $"fence drag fenced {FenceCount() - before} cells, expected {expected.Count}");
        foreach (int c in expected) Check(W.Fence[c] == 1, $"cell {c % N},{c / N} of the line is not fenced");
        Check(_main.Audio.Places == places + 1, "no place sound cue for the fence");
        Check(_main.Edits.WattlePieces > 0, "fence not drawn");
        _notes.Add($"fence={expected.Count}");
    }

    private async Task Bridge()
    {
        TestInput.Key(Key.B);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Bridge, "B did not pick up the footbridge tool");
        // Somewhere no bridge fits: a reason in words and a cross, not a bridge.
        TestInput.Motion(TestInput.CellScreen(_main, 3, 3));
        await TestInput.Frames(this, 2);
        Check(_main.Tools.LastMessage.Contains("river"), $"no reason given off the river: '{_main.Tools.LastMessage}'");
        Check(_main.Tools.Ghost.Pieces == 1, "no cross where no bridge fits");
        int row = -1;
        var cells = new List<int>();
        for (int y = 12; y < N - 12 && row < 0; y++)
            if (Math.Abs(y - W.Map.FordY) > 8 && W.BridgeCellsAt(W.RiverX[y] + 0.5, y + 0.5, cells)) row = y;
        Check(row >= 0, "no row for a test bridge");
        var at = TestInput.CellScreen(_main, (int)W.RiverX[row], row);
        TestInput.Motion(at);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Ghost.Pieces == cells.Count, $"bridge ghost shows {_main.Tools.Ghost.Pieces} cells, expected {cells.Count}");
        TestInput.Button(at, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        Check(W.Bridges.Count == 0, "bridge built on press");
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(W.Bridges.Count == 1 && W.Bridges[0].Cells.Count == cells.Count && W.Bridges[0].Cells[0] == cells[0], "footbridge not built where shown");
        // The success holds the status line a moment, though the spot under the pointer now has a bridge on it.
        TestInput.Motion(at + new Vector2(0, 6));
        await TestInput.Frames(this, 2);
        Check(_main.Tools.LastMessage.StartsWith("Footbridge built") && _main.Tools.HoldingSuccess, $"the success gave way at once to '{_main.Tools.LastMessage}'");
        _notes.Add($"bridge={cells.Count}@{row}");
    }

    private async Task Erase()
    {
        TestInput.Key(Key.E);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Erase, "E did not pick up the eraser");
        int erases = _main.Audio.Erases;
        // Click one fence cell: the 3 x 3 brush round it goes.
        int first = -1;
        for (int c = 0; c < W.Grid.Count && first < 0; c++) if (W.Fence[c] == 1) first = c;
        int before = FenceCount();
        var p = TestInput.CellScreen(_main, first % N, first / N);
        TestInput.Motion(p);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 1);
        Check(FenceCount() < before && FenceCount() > 0, $"a click erased {before - FenceCount()} of {before} fence cells");
        // Drag along the rest of the line: all of it goes.
        int a = -1, b = -1;
        for (int c = 0; c < W.Grid.Count; c++) if (W.Fence[c] == 1) { if (a < 0) a = c; b = c; }
        await Drag(TestInput.CellScreen(_main, a % N, a / N), TestInput.CellScreen(_main, b % N, b / N), 10);
        TestInput.Button(TestInput.CellScreen(_main, b % N, b / N), MouseButton.Left, false);
        await TestInput.Frames(this, 1);
        Check(FenceCount() == 0, $"{FenceCount()} fence cells left after an erase drag");
        // A click on the bridge removes the whole bridge.
        var bc = W.Bridges[0].Cells[1];
        p = TestInput.CellScreen(_main, bc % N, bc / N);
        TestInput.Motion(p);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 1);
        Check(W.Bridges.Count == 0, "bridge not erased");
        Check(_main.Audio.Erases > erases, "no erase sound cue");
        _notes.Add("erase=ok");
    }

    private async Task Toolbar()
    {
        // Zoom in so the bookmark tabs lie over the map: a press there must reach the tab, never the map.
        _main.Rig.LookAt(new Vector3(128, 0, 128), 90f);
        await TestInput.Frames(this, 2);
        TestInput.Key(Key.F);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Fence, "fence tool not in hand");
        var tab = (Control)_main.Ui.Controls["WearOverlay"];
        var at = TestInput.Centre(tab);
        Check(_main.Rig.PickCell(at, out _, out _), "the tab is not over the map in this view (test setup)");
        bool overlay = _main.Ui.Overlay;
        TestInput.Motion(at);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.PointerOverUi && _main.Tools.Ghost.Pieces == 0, "ghost shown while the pointer is over a tab");
        TestInput.Button(at, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(FenceCount() == 0, "a click on a tab placed a fence");
        Check(_main.Ui.Overlay != overlay, "the tab did not get the click");
        TestInput.Key(Key.W);
        await TestInput.Frames(this, 1);
        // A drag from the map released over a tab is cancelled.
        var start = _main.Rig.LocalToScreen(new Vector3(100, 0, 120));
        await Drag(start, at, 4);
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(FenceCount() == 0, "a drag released over a tab placed a fence");
        Check(_main.Tools.LastMessage.Contains("cancelled"), $"no cancel message: '{_main.Tools.LastMessage}'");
        // A right click puts the tool down; Escape too.
        TestInput.Motion(start);
        TestInput.Button(start, MouseButton.Right, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(start, MouseButton.Right, false);
        await TestInput.Frames(this, 1);
        Check(_main.Tools.Tool == Tool.None, "right click did not put the tool down");
        TestInput.Key(Key.B);
        await TestInput.Frames(this, 1);
        TestInput.Key(Key.Escape);
        await TestInput.Frames(this, 1);
        Check(_main.Tools.Tool == Tool.None, "Escape did not put the tool down");
        Check(FenceCount() == 0 && W.Bridges.Count == 0, "something was built during the toolbar checks");
        _main.Rig.FramePlay(instant: true);
        _notes.Add("toolbar=ok");
    }

    /// <summary>Same seed, same ticks, with and without preview activity between them: identical wear and random stream.</summary>
    private async Task Isolation()
    {
        const int seed = 3, frames = 120, ticksPerFrame = 3;
        var sim = _main.Sim;
        sim.Reset(seed);
        sim.StepTicks(6 * _main.Params.TicksPerDay);
        await TestInput.Frames(this, 1);
        // Run A: a fence drag swept over the busiest ground, then footbridge hovering, previews recomputing throughout.
        var (hx, hy) = Busiest();
        TestInput.Key(Key.F);
        await TestInput.Frames(this, 1);
        int done0 = _main.Tools.Previewer.Completed;
        await Drag(TestInput.CellScreen(_main, hx - 6, hy), TestInput.CellScreen(_main, hx + 6, hy), 2);
        for (int f = 0; f < frames; f++)
        {
            sim.StepTicks(ticksPerFrame);
            if (f == frames / 2) { TestInput.Key(Key.Escape); TestInput.Key(Key.B); }
            // The pointer rests a while at each spot (as a player's does), so previews can finish between moves.
            int wob = (f / 15 % 4) * 3 - 4;
            int row = Math.Clamp(hy + wob, 2, N - 3);
            var p = f < frames / 2 ? TestInput.CellScreen(_main, hx + 6, hy + wob) : TestInput.CellScreen(_main, (int)W.RiverX[row], row);
            TestInput.Motion(p);
            await TestInput.Frames(this, 1);
        }
        TestInput.Key(Key.Escape);
        await TestInput.Frames(this, 1);
        int previews = _main.Tools.Previewer.Completed - done0;
        Check(previews >= 3, $"only {previews} previews finished during the isolation run ({_main.Tools.Previewer.Steps} steps, {_main.Tools.PreviewSkips} frames skipped, longest step {_main.Tools.Previewer.StepMs:F1} ms)");
        Check(FenceCount() == 0 && W.Bridges.Count == 0, "the isolation run built something");
        ulong a = Hash(W);
        // Run B: no tools.
        sim.Reset(seed);
        sim.StepTicks(6 * _main.Params.TicksPerDay);
        await TestInput.Frames(this, 1);
        for (int f = 0; f < frames; f++) { sim.StepTicks(ticksPerFrame); await TestInput.Frames(this, 1); }
        ulong b = Hash(W);
        Check(a == b, $"flow preview changed the run: wear hash {a:x16} with previews, {b:x16} without");
        _notes.Add($"isolation previews={previews} hash={a:x16}");
    }

    private (int, int) Busiest()
    {
        int best = 0;
        float bw = -1;
        for (int y = 10; y < N - 10; y++)
            for (int x = 10; x < N - 10; x++)
            {
                int c = y * N + x;
                if (W.Wear[c] > bw && W.Water[c] == 0) { bw = W.Wear[c]; best = c; }
            }
        return (best % N, best / N);
    }

    private static ulong Hash(World w)
    {
        ulong h = 1469598103934665603UL;
        foreach (float v in w.Wear) { h ^= BitConverter.SingleToUInt32Bits(v); h *= 1099511628211UL; }
        foreach (byte v in w.Hollow) { h ^= v; h *= 1099511628211UL; }
        h ^= w.RngState; h *= 1099511628211UL;
        h ^= (ulong)w.Ticks; h *= 1099511628211UL;
        return h;
    }

    private static void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }
}
