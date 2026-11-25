using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// The gravel and gate tools through real input (step 2 of the game round), with the simulation's state checked:
/// - gravel: G, a drag along a worn trail brushes a strip (ghost and flow preview during the drag, snapped to the trail,
///   nothing laid before release), release lays exactly the brushed cells, and the ground draws them;
/// - gate: on a fence drawn at an angle (a staircase), T and a hover near it finds a gate (one cell or a stair corner's
///   two) with its preview; press and release puts it in and the gate is drawn; a hover far from fences and over a lone
///   post gives the toy's reasons in words; a click on the gate takes it out;
/// - erase: a click removes gravel, and a fence cell with its gate.
/// </summary>
public partial class ToolsTest
{
    private int Count(ReadOnlySpan<byte> g) { int n = 0; foreach (byte b in g) n += b != 0 ? 1 : 0; return n; }

    private async Task Click(Vector2 p)
    {
        TestInput.Motion(p);
        await TestInput.Frames(this, 2);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
    }

    /// <summary>A map cell with no fence within 3 cells.</summary>
    private int FarFromFences()
    {
        for (int y = 20; y < N - 20; y++)
            for (int x = 10; x < N - 10; x++)
            {
                bool clear = true;
                for (int dy = -3; dy <= 3 && clear; dy++)
                    for (int dx = -3; dx <= 3 && clear; dx++) clear = W.Fence[(y + dy) * N + x + dx] == 0;
                if (clear) return y * N + x;
            }
        throw new InvalidOperationException("no cell away from fences");
    }

    private async Task Gravel()
    {
        TestInput.Key(Key.G);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Gravel, "G did not pick up the gravel tool");
        // A worn trail away from the river: its busiest cell, then the cell 5 further along its wear.
        int a = -1; float best = (float)W.Params.ThresholdTrail;
        for (int c = 0; c < W.Grid.Count; c++)
        {
            int x = c % N, y = c / N;
            if (x < 10 || y < 10 || x > N - 10 || y > N - 10 || Math.Abs(x - W.RiverX[y]) < 10 || !W.CanGravel(c)) continue;
            if (W.Wear[c] > best) { best = W.Wear[c]; a = c; }
        }
        Check(a >= 0, "no worn trail to gravel");
        int b = a, prev = -1;
        for (int i = 0; i < 5; i++)
        {
            int next = -1; float nw = -1;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int v = b + dy * N + dx;
                    if (v == b || v == prev || v == a || !W.CanGravel(v)) continue;
                    if (W.Wear[v] > nw) { nw = W.Wear[v]; next = v; }
                }
            if (next < 0) break;
            prev = b; b = next;
        }
        int before = Count(W.Gravel), places = _main.Audio.Places;
        await Drag(TestInput.CellScreen(_main, a % N, a / N), TestInput.CellScreen(_main, b % N, b / N), 8);
        int brushed = _main.Tools.BrushCells;
        Check(brushed >= 3, $"the brush took {brushed} cells along the trail");
        Check(_main.Tools.Ghost.Pieces == brushed, $"gravel ghost shows {_main.Tools.Ghost.Pieces}, brush has {brushed}");
        Check(Count(W.Gravel) == before, "gravel laid before release");
        for (int i = 0; i < 120 && !_main.Tools.Previewer.Settled; i++) await TestInput.Frames(this, 1);
        Check(_main.Tools.Previewer.Settled && _main.Tools.Previewer.Text.Length > 0, $"no flow preview for the stroke: '{_main.Tools.Previewer.Text}'");
        string readout = _main.Tools.Previewer.Text;
        TestInput.Button(TestInput.CellScreen(_main, b % N, b / N), MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(Count(W.Gravel) == before + brushed, $"release laid {Count(W.Gravel) - before} cells, brushed {brushed}");
        Check(_main.Tools.LastMessage == $"Gravel laid ({brushed} cells).", $"message '{_main.Tools.LastMessage}'");
        Check(W.Gravel[W.GravelSnap(a)] != 0 || W.Gravel[a] != 0, "the stroke's first cell has no gravel");
        Check(_main.Ground.GravelCells == Count(W.Gravel), $"ground draws {_main.Ground.GravelCells} gravel cells of {Count(W.Gravel)}");
        Check(_main.Audio.Places == places + 1, "no place cue for gravel");
        _notes.Add($"gravel={brushed} '{readout}'");
    }

    private async Task Gate()
    {
        // A fence at an angle (a 4-connected staircase) across open ground, drawn with the fence tool.
        var (x0, y0) = FreeRun(40, 14);
        int x1 = x0 + 6, y1 = y0 + 4;
        var line = new List<int>();
        W.Grid.Line4(x0, y0, x1, y1, line);
        TestInput.Key(Key.F);
        await TestInput.Frames(this, 2);
        await Drag(TestInput.CellScreen(_main, x0, y0), TestInput.CellScreen(_main, x1, y1));
        TestInput.Button(TestInput.CellScreen(_main, x1, y1), MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        foreach (int c in line) Check(W.Fence[c] == 1, "test staircase fence not placed");
        TestInput.Key(Key.T);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Gate, "T did not pick up the gate tool");
        // Far from any fence: the toy's reason.
        int far = FarFromFences();
        TestInput.Motion(TestInput.CellScreen(_main, far % N, far / N));
        await TestInput.Frames(this, 2);
        Check(_main.Tools.LastMessage == FenceGates.NoFence, $"no-fence reason: '{_main.Tools.LastMessage}'");
        // Near the middle of the staircase: a gate of one cell or a corner's two, with a preview.
        int mid = line[line.Count / 2];
        var at = TestInput.CellScreen(_main, mid % N, mid / N);
        TestInput.Motion(at);
        await TestInput.Frames(this, 2);
        var cells = _main.Tools.GateCells.ToArray();
        Check(cells.Length is 1 or 2, $"gate target has {cells.Length} cells");
        foreach (int c in cells) Check(W.Fence[c] == 1, "gate target is not on the fence");
        for (int i = 0; i < 120 && !_main.Tools.Previewer.Settled; i++) await TestInput.Frames(this, 1);
        string readout = _main.Tools.Previewer.Text;
        TestInput.Button(at, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        Check(W.Gate[cells[0]] == 0, "gate placed on press");
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        foreach (int c in cells) Check(W.Gate[c] == 1, $"cell {c % N},{c / N} is not a gate after the click ({_main.Tools.LastMessage})");
        Check(_main.Tools.LastMessage == "Gate placed.", $"message '{_main.Tools.LastMessage}'");
        Check(_main.Edits.GatePieces == 1, $"{_main.Edits.GatePieces} gates drawn");
        // A click on the gate takes it out; another puts it back.
        await Click(at);
        Check(W.Gate[cells[0]] == 0 && _main.Tools.LastMessage == "Gate removed.", $"gate not removed: '{_main.Tools.LastMessage}'");
        Check(_main.Edits.GatePieces == 0, "a removed gate is still drawn");
        await Click(at);
        Check(W.Gate[cells[0]] == 1, "gate not put back");
        // A lone post: the reason in words.
        int lone = -1;
        for (int y = 20; y < N - 20 && lone < 0; y++)
            for (int x = 10; x < N - 10 && lone < 0; x++)
            {
                bool clear = true;
                for (int dy = -3; dy <= 3 && clear; dy++)
                    for (int dx = -3; dx <= 3 && clear; dx++) clear = W.CanFence((y + dy) * N + x + dx) && W.Fence[(y + dy) * N + x + dx] == 0;
                if (clear) lone = y * N + x;
            }
        _main.Sim.PlaceFence(new[] { lone });
        TestInput.Motion(TestInput.CellScreen(_main, lone % N, lone / N));
        await TestInput.Frames(this, 2);
        Check(_main.Tools.LastMessage == FenceGates.LonePost, $"lone-post reason: '{_main.Tools.LastMessage}'");
        _notes.Add($"gate={cells.Length} '{readout}'");

        // Erase: a click on the gate removes its fence cells and the gate; a click on the gravel removes it.
        TestInput.Key(Key.E);
        await TestInput.Frames(this, 2);
        await Click(at);
        Check(W.Gate[cells[0]] == 0 && W.Fence[cells[0]] == 0, "erase left the gate");
        Check(_main.Edits.GatePieces == 0, "an erased gate is still drawn");
        int g = -1;
        for (int c = 0; c < W.Grid.Count && g < 0; c++) if (W.Gravel[c] != 0) g = c;
        int gravel = Count(W.Gravel);
        await Click(TestInput.CellScreen(_main, g % N, g / N));
        Check(Count(W.Gravel) < gravel && W.Gravel[g] == 0, "erase did not take the gravel up");
        Check(_main.Ground.GravelCells == Count(W.Gravel), "the ground still draws erased gravel");
        _notes.Add($"erase-paths=ok");
        // Leave the map as the next test expects it: no fences, gates or gravel.
        var left = new List<int>();
        for (int c = 0; c < W.Grid.Count; c++) if (W.Fence[c] != 0 || W.Gravel[c] != 0) left.Add(c);
        _main.Sim.Erase(left.ToArray());
        TestInput.Key(Key.Escape);
        await TestInput.Frames(this, 2);
    }
}
