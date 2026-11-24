using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless smoke test: loads the main scene, runs about 120 frames, checks the book and
/// camera exist and that the sim library runs inside the game, then quits with 0 or 1.
/// Godot logs unhandled C# exceptions without changing the exit code, so every failure
/// path here ends in an explicit Quit(1).
/// </summary>
public partial class Smoke : Node
{
    private const int Frames = 120;
    private int _frame;
    private Main? _main;

    public override void _Ready()
    {
        try
        {
            _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
            AddChild(_main);
        }
        catch (Exception e) { Fail("main scene did not load: " + e.Message); }
    }

    public override void _Process(double delta)
    {
        if (++_frame < Frames) return;
        try
        {
            if (_main == null) { Fail("no main scene"); return; }
            foreach (var path in new[] { "Book/LeftPage/Paper", "Book/RightPage/MapOrigin/Map", "Table/Top", "CameraRig/Yaw/Pitch/Camera", "Hud", "Sun" })
                if (_main.GetNodeOrNull(path) == null) { Fail("missing node " + path); return; }
            if (!_main.Rig.Camera.Current) { Fail("camera is not current"); return; }

            var p = _main.Params;
            var grid = p.Grid;
            var cost = new float[grid.Count];
            Array.Fill(cost, 1f);
            var field = new float[grid.Count];
            CostField.Dijkstra(grid, grid.Idx(0, 0), cost, field, new BinaryHeap(1024));
            float far = field[grid.Idx(grid.Width - 1, grid.Height - 1)];
            float expect = (float)((grid.Width - 1) * Math.Sqrt(2));
            if (Math.Abs(far - expect) > 0.01f) { Fail($"sim field wrong: {far} vs {expect}"); return; }

            // The map's far corner cell must land inside the right-hand page.
            var corner = _main.CellToWorld(grid.Width - 1, grid.Height - 1);
            if (corner.X <= 0 || corner.X > 2 * BookBuilder.PageWidth) { Fail($"map corner off the page: {corner}"); return; }

            GD.Print($"SMOKE PASS frames={_frame} renderer={RenderingServer.GetCurrentRenderingMethod()} field={far:F2} corner={corner}");
            GetTree().Quit(0);
        }
        catch (Exception e) { Fail(e.ToString()); }
    }

    private void Fail(string why)
    {
        GD.PrintErr("SMOKE FAIL: " + why);
        GetTree().Quit(1);
    }
}
