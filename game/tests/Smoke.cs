using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless smoke test of the world view. Loads the main scene and checks, in order:
/// the scene tree is complete; the frame loop runs the simulation; after 300 more ticks walkers have moved, wear
/// is above zero and the walker MultiMesh draws them; a fence and a bridge placed through the debug commands
/// appear as nodes (and go again on erase); picking returns the expected cell for known screen points at two
/// zooms and off-map points return none; scenery stays off doors and the ford; reset and new map rebuild.
/// Godot logs unhandled C# exceptions without changing the exit code, so every failure ends in Quit(1); the
/// check script also fails the step if the log holds any error line.
/// </summary>
public partial class Smoke : Node
{
    private Main _main = null!;
    private int _frame;
    private long _ticksAtStart;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        try
        {
            // Headless windows are 64 x 64; picking and framing are checked at the game's real window shape.
            GetWindow().Size = new Vector2I(1280, 720);
            _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
            AddChild(_main);
        }
        catch (Exception e) { Fail("main scene did not load: " + e.Message); }
    }

    public override void _Process(double delta)
    {
        _frame++;
        try
        {
            if (_frame == 5) { _ticksAtStart = _main.Sim.World.Ticks; _main.Sim.SetSpeed(8); }
            if (_frame < 60) return;
            Run();
            GD.Print($"SMOKE PASS frames={_frame} renderer={RenderingServer.GetCurrentRenderingMethod()} " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e) { Fail(e.ToString()); }
        SetProcess(false);
    }

    private void Run()
    {
        string[] paths =
        {
            "Book/LeftPage/Paper", "Book/LeftPage/Folio", "Hud/Root/Chapter", "Hud/Root/Tabs", "Tools", "Audio", "Book/RightPage/Paper", "Book/RightPage/Stack",
            "Book/RightPage/MapOrigin/Ground", "Book/RightPage/MapOrigin/Town", "Book/RightPage/MapOrigin/Scenery",
            "Book/RightPage/MapOrigin/Edits/Fences/Wattle", "Book/RightPage/MapOrigin/Edits/Bridges",
            "Book/RightPage/MapOrigin/Walkers", "Book/RightPage/MapOrigin/Ghost", "Book/RightPage/MapOrigin/Routes", "Table/Top", "CameraRig/Yaw/Pitch/Camera", "CameraRig/Yaw/Pitch/Camera/Ink/InkHard",
            "CameraRig/Yaw/Pitch/Camera/Ink/InkSoft", "Hud", "Sun", "Sim",
        };
        foreach (var p in paths) Check(_main.GetNodeOrNull(p) != null, "missing node " + p);
        Check(_main.Rig.Camera.Current, "camera is not current");
        var sim = _main.Sim;
        var w = sim.World;
        Check(sim.World.Ticks > _ticksAtStart, "the frame loop ran no ticks");
        Check(_main.Town.GetChildCount() == w.Buildings.Length, $"town has {_main.Town.GetChildCount()} nodes for {w.Buildings.Length} buildings");

        // Same seed, same town as the toy: seed 1's map is fixed by the golden tests; check its shape here.
        Check(w.Seed == 1 && w.Buildings.Length >= 7, "seed 1 should have at least 7 buildings");

        // 300 ticks: walkers move, wear grows, walkers are drawn.
        var before = new (double, double)[w.WalkerCount];
        for (int i = 0; i < before.Length; i++) before[i] = (w.Walkers[i].X, w.Walkers[i].Y);
        sim.StepTicks(300);
        int moved = 0;
        for (int i = 0; i < before.Length; i++) if (before[i] != (w.Walkers[i].X, w.Walkers[i].Y)) moved++;
        double wear = 0;
        foreach (float v in w.Wear) wear += v;
        Check(moved > w.WalkerCount / 4, $"only {moved} of {w.WalkerCount} walkers moved in 300 ticks");
        Check(wear > 0, "no wear after 300 ticks");
        _main.Walkers.Sync();
        Check(_main.Walkers.Drawn == w.WalkersOut(), $"walker layer drew {_main.Walkers.Drawn}, sim has {w.WalkersOut()} out");
        Check(_main.Walkers.Drawn > 0, "no walkers drawn");
        int up = _main.Ground.Uploads;
        _main.Ground.Upload();
        Check(_main.Ground.Uploads == up + 1, "wear upload did not run");
        _notes.Add($"moved={moved} wear={wear:F0} drawn={_main.Walkers.Drawn}");

        CheckEdits(sim);
        CheckPicking();
        CheckScenery(w);

        // Reset keeps the seed and starts over; new map moves to the toy's next seed and rebuilds the town.
        sim.Reset();
        Check(sim.World.Ticks == 0 && sim.World.Seed == 1, "reset did not start over");
        sim.NewMap();
        Check(sim.Seed == SimDriver.NextSeed(1), "new map did not take the next seed");
        Check(_main.Town.GetChildCount() == sim.World.Buildings.Length, "town not rebuilt for the new map");
        Check(_main.Edits.GetNode("Bridges").GetChildCount() == 0, "bridges survived a new map");
        _notes.Add($"newseed={sim.Seed}");
    }

    private void CheckEdits(SimDriver sim)
    {
        var w = sim.World;
        int n = w.Grid.Width;
        // A fence: the first run of 8 fenceable cells on row 100, found from the map so it holds on any seed.
        int row = 100, x0 = -1;
        for (int x = 2; x < n - 10 && x0 < 0; x++)
        {
            bool ok = true;
            for (int k = 0; k < 8; k++) ok &= w.CanFence(row * n + x + k);
            if (ok) x0 = x;
        }
        Check(x0 >= 0, "no room for a test fence");
        int rebuilds = _main.Edits.Rebuilds;
        Check(sim.PlaceFenceLine(x0, row, x0 + 7, row) == 8, "fence not placed");
        Check(_main.Edits.Rebuilds == rebuilds + 1, "fence did not rebuild the edit layer");
        Check(_main.Edits.WattlePieces >= 7 && _main.Edits.PostPieces >= 2, $"fence pieces: wattle {_main.Edits.WattlePieces}, posts {_main.Edits.PostPieces}");

        // A bridge: the first row well away from the ford where one fits.
        Bridge? b = null;
        for (int y = 10; y < n - 10 && b == null; y++)
            if (Math.Abs(y - w.Map.FordY) > 6) b = sim.PlaceBridge((int)w.RiverX[y], y);
        Check(b != null, "no bridge fits anywhere");
        var node = _main.Edits.GetNodeOrNull<Node3D>($"Bridges/Bridge{b!.Id}");
        Check(node != null, "bridge node missing");
        Check(node!.GetChildCount() == b.Cells.Count + 2, $"bridge has {node.GetChildCount()} pieces for {b.Cells.Count} cells");
        _notes.Add($"fence={_main.Edits.WattlePieces}w/{_main.Edits.PostPieces}p bridge={b.Cells.Count}+2");

        // Erase both: the pieces go.
        var cells = new List<int>();
        for (int k = 0; k < 8; k++) cells.Add(row * n + x0 + k);
        cells.Add(b.Cells[0]);
        Check(sim.Erase(cells.ToArray()) == 9, "erase did not remove the fence and the bridge");
        Check(_main.Edits.WattlePieces == 0 && _main.Edits.GetNode("Bridges").GetChildCount() == 0, "pieces left after erase");
    }

    private void CheckPicking()
    {
        var rig = _main.Rig;
        int checkedPoints = 0;
        foreach (float size in new[] { 268.8f, 60f })
        {
            rig.LookAt(new Vector3(100, 0, 140), size);
            // Points inside known cells (centre and near corners), projected to the screen and picked back.
            int[][] cells = { new[] { 50, 70 }, new[] { 51, 70 }, new[] { 50, 71 }, new[] { 45, 66 }, new[] { 55, 75 } };
            foreach (var c in cells)
                foreach (var off in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.8f) })
                {
                    var local = new Vector3((c[0] + off.X) * 2f, 0, (c[1] + off.Y) * 2f);
                    var screen = rig.LocalToScreen(local);
                    Check(rig.PickCell(screen, out int px, out int py), $"pick at {screen} returned no cell (size {size})");
                    Check(px == c[0] && py == c[1], $"picked ({px},{py}) for cell ({c[0]},{c[1]}) at size {size}");
                    checkedPoints++;
                }
        }
        // Off the map: a point west of the map's edge (on the margin of the page) picks nothing.
        rig.LookAt(new Vector3(0, 0, 128), 268.8f);
        var outside = rig.LocalToScreen(new Vector3(-10, 0, 128));
        Check(!rig.PickCell(outside, out _, out _), "a point off the map picked a cell");
        // Zoom at the cursor keeps the map point under it; pans and zooms stop at their limits.
        rig.LookAt(new Vector3(128, 0, 128), 200f);
        var vp = GetViewport().GetVisibleRect().Size;
        var cursor = vp * new Vector2(0.33f, 0.42f);
        rig.PickLocal(cursor, out var before);
        rig.ZoomAt(cursor, 0.5f);
        for (int i = 0; i < 120; i++) rig._Process(1 / 60.0);
        rig.PickLocal(cursor, out var after);
        Check(Mathf.IsEqualApprox(rig.Camera.Size, 100f, 0.01f), $"zoom ended at {rig.Camera.Size}, not 100");
        Check(before.DistanceTo(after) < 0.05f, $"zoom at cursor drifted {before.DistanceTo(after):F3} m");
        rig.LookAt(new Vector3(-5000, 0, 9000), 1f);
        var p = rig.PivotLocal;
        Check(rig.Camera.Size >= rig.MinSize && p.X >= -rig.PanMargin && p.Z <= rig.MapSize + rig.PanMargin, $"pan or zoom passed its limit: {p}, size {rig.Camera.Size}");
        rig.FramePlay(instant: true);
        _notes.Add($"viewport={vp.X}x{vp.Y} picks={checkedPoints} zoomdrift={before.DistanceTo(after):F4}m");
    }

    private void CheckScenery(World w)
    {
        int n = w.Grid.Width;
        Check(_main.Scenery.TreeCells.Count > 30, $"only {_main.Scenery.TreeCells.Count} trees");
        foreach (var (cell, _) in _main.Scenery.TreeCells)
        {
            Check(w.Ford[cell] == 0 && w.Water[cell] == 0, $"tree on water or ford at {cell % n},{cell / n}");
            foreach (var b in w.Buildings) Check(b.Door != cell, $"tree on a door at {cell % n},{cell / n}");
        }
        _notes.Add($"trees={_main.Scenery.TreeCells.Count}");
    }

    private void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }

    private void Fail(string why)
    {
        GD.PrintErr("SMOKE FAIL: " + why);
        GetTree().Quit(1);
        SetProcess(false);
    }
}
