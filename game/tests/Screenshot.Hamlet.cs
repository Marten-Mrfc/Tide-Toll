using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Chapter I's screenshots (both window sizes, as NAME-WxH.png), seed 1, 200 walkers:
/// - hamlet-day1: the book on day one, Chapter I's page with its how-to and the chapter bookmark;
/// - chapter-turn: the page-turn moment half way (the leaf over the book, the next chapter's heading).
/// "-- --hamlet-only" takes only these.
/// </summary>
public partial class Screenshot
{
    private const string HamletNames = "hamlet-day1 chapter-turn hamlet-paths hamlet-paths-close hamlet-gravel-preview hamlet-day30 hamlet-newhouse hamlet-rain hamlet-rain-close hamlet-mud-drying";

    private async Task HamletShots()
    {
        if (_main.Chapter != 1) { _main.SwitchChapter(1); await Frames(2); }
        _main.Sim.Reset(1);
        _main.Sim.SetPaused(false);
        _main.Rig.FrameBook(instant: true);
        await Frames(10);
        Check(await Snap("hamlet-day1", true), "hamlet-day1");

        // The page turn, caught while the leaf covers about half the book.
        _main.Turn.PinAt = PageTurn.CoverSec * 0.62;
        _main.TurnPage();
        await Frames(4);
        Check(await Snap("chapter-turn", true), "chapter-turn");
        _main.Turn.PinAt = -1;
        for (int i = 0; i < 400 && _main.Turn.Busy; i++) await Frames(1);
        if (_main.Chapter != 2) { Quit(1, "the page turn did not open Chapter II"); return; }
        _main.SwitchChapter(1);
        await Frames(4);
        await PathShots();
        await GrowthShots();
        await RainShots();
    }

    /// <summary>
    /// A forced shower on the day-31 town (World.StartRain): the play view with the rain over the map page, wet ground
    /// and muddy trails, and a close look; then half way through the mud's drying, where the lighter trails have dried.
    /// </summary>
    private async Task RainShots()
    {
        var sim = _main.Sim;
        var w = sim.World;
        var p = _main.Params;
        sim.SetPaused(true);
        w.StartRain(p.DaySimSec);
        sim.StepTicks((int)(p.RainRampSimSec * p.TickHz) + 20);
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.FramePlay(instant: true);
        sim.SetPaused(false);
        for (int i = 0; i < 200 && _main.Ground.Wet < 0.99f; i++) await Frames(1); // the ground wets over about a second
        sim.SetPaused(true);
        GD.Print($"SCREENSHOT hamlet-rain: mud cells {w.MudCells}, rain {_main.Ui.Rain.Level:F2}, wet {_main.Ground.Wet:F2}, weather '{_main.Ui.Chapter.WeatherText}'");
        if (_main.Ui.Rain.Level <= 0 || w.MudCells == 0) { Quit(1, "no rain or no mud to photograph"); return; }
        Check(await Snap("hamlet-rain", true), "hamlet-rain");
        var well = w.Buildings[w.OfKind(BuildingKind.Well)[0]];
        _main.Rig.LookAt(new Vector3((well.X + 1) * 2f + 8f, 0, (well.Y + 1) * 2f + 12f), 70f);
        await Frames(3);
        Check(await Snap("hamlet-rain-close", true), "hamlet-rain-close");
        w.StopRain();
        double at = w.Weather.MudUntil - p.DrySimSec * 0.45;
        while (w.Time < at) sim.StepTicks(10);
        _main.Ground.Upload();
        sim.SetPaused(false);
        for (int i = 0; i < 200 && Math.Abs(_main.Ground.Wet - (float)w.Weather.Wetness) > 0.02f; i++) await Frames(1);
        sim.SetPaused(true);
        GD.Print($"SCREENSHOT hamlet-mud-drying: wetness {w.Weather.Wetness:F2}, wettest mud drawn {_main.Ground.MudShownMax:F2}, weather '{_main.Ui.Chapter.WeatherText}'");
        Check(await Snap("hamlet-mud-drying", true), "hamlet-mud-drying");
    }

    /// <summary>
    /// Day 30 of seed 1 with the houses the town grew (the play view and the whole book), then the moment just after a
    /// new cottage is raised: its rings and badge, close up.
    /// </summary>
    private async Task GrowthShots()
    {
        var sim = _main.Sim;
        sim.Reset(1);
        sim.SetPaused(true);
        int day = _main.Params.TicksPerDay;
        while (sim.World.Stats().Day < 30) sim.StepTicks(day / 4);
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.FramePlay(instant: true);
        await Frames(4 + (int)(TownLayer.RaiseSec * 4));
        GD.Print($"SCREENSHOT hamlet-day30: houses {sim.World.Houses.Length} (grown {sim.World.Grown.Length}, drawn {_main.Town.Grown}), " +
                 $"walkers {sim.World.WalkerCount}, felled trees {_main.Scenery.Felled}, weather '{_main.Ui.Chapter.WeatherText}'");
        if (sim.World.Grown.Length == 0 || _main.Town.Grown != sim.World.Grown.Length) { Quit(1, "no grown houses drawn by day 30"); return; }
        Check(await Snap("hamlet-day30", true), "hamlet-day30");
        // Run on until the next house is built, then look at it a moment after it stood up.
        int g = sim.World.Grown.Length;
        for (int i = 0; i < 40 * day && sim.World.Grown.Length == g; i++) sim.StepTicks(1);
        if (sim.World.Grown.Length == g) { Quit(1, "no new house after day 30"); return; }
        var b = sim.World.Buildings[sim.World.Grown[g].Building];
        _main.Rig.LookAt(new Vector3((b.X + b.W / 2f) * 2f, 0, (b.Y + b.H / 2f) * 2f + 6f), 90f);
        double t0 = _main.Town.Now;
        while (_main.Town.Now - t0 < TownLayer.RaiseSec + 0.6) await Frames(1);
        GD.Print($"SCREENSHOT hamlet-newhouse: house {sim.World.Grown[g].Building} at {b.X},{b.Y}, marks showing {_main.Ui.Houses.Showing}");
        Check(await Snap("hamlet-newhouse", true), "hamlet-newhouse");
    }

    /// <summary>
    /// A fence across the busiest trail below the well with a gate in it, and a gravel strip brushed along another
    /// trail, after ten days; the play view and a close look; then the gravel tool hovering a trail with its preview.
    /// </summary>
    private async Task PathShots()
    {
        var sim = _main.Sim;
        sim.Reset(1);
        sim.SetPaused(true);
        sim.StepTicks(10 * _main.Params.TicksPerDay);
        var w = sim.World;
        int n = w.Grid.Width;
        // The busiest open cell with room for a 9-cell fence across it (east-west), away from the river.
        int best = -1; float bw = -1;
        for (int y = 12; y < n - 12; y++)
            for (int x = 12; x < n - 12; x++)
            {
                int c = y * n + x;
                if (Math.Abs(x - w.RiverX[y]) < 12) continue;
                bool clear = true;
                for (int k = -4; k <= 4 && clear; k++) clear = w.CanFence(c + k) && w.Fence[c + k] == 0;
                if (clear && w.Wear[c] > bw) { bw = w.Wear[c]; best = c; }
            }
        int fx = best % n, fy = best / n;
        sim.PlaceFenceLine(fx - 4, fy, fx + 4, fy + 2);
        int gateCell = -1;
        for (int k = -1; k <= 1 && gateCell < 0; k++)
            for (int dy = 0; dy <= 2 && gateCell < 0; dy++)
                if (w.GateCellsFor(Math.Clamp(fy + dy, 0, n - 1) * n + fx + k) != null) gateCell = (fy + dy) * n + fx + k;
        if (gateCell < 0 || !sim.PlaceGate(gateCell)) { Quit(1, "no gate fits in the test fence"); return; }
        // Gravel along a trail: from the busiest worn cells well away from the fence, ten cells brushed by snap.
        int g0 = -1; float gw = -1;
        for (int c = 0; c < n * n; c++)
        {
            int x = c % n, y = c / n;
            if (Math.Abs(x - fx) + Math.Abs(y - fy) < 16 || Math.Abs(x - w.RiverX[y]) < 8 || !w.CanGravel(c)) continue;
            if (w.Wear[c] > gw) { gw = w.Wear[c]; g0 = c; }
        }
        var strip = new System.Collections.Generic.List<int>();
        int cur = g0;
        for (int i = 0; i < 12 && cur >= 0; i++)
        {
            strip.Add(cur);
            int next = -1; float nw = 20;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int v = cur + dy * n + dx;
                    if (v < 0 || v >= n * n || strip.Contains(v) || !w.CanGravel(v)) continue;
                    if (w.Wear[v] > nw) { nw = w.Wear[v]; next = v; }
                }
            cur = next;
        }
        int laid = sim.PlaceGravel(strip.ToArray());
        GD.Print($"SCREENSHOT paths: fence {fx - 4},{fy} to {fx + 4},{fy + 2}, gate at {gateCell % n},{gateCell / n}, gravel {laid} cells from {g0 % n},{g0 / n}, " +
                 $"gates drawn {_main.Edits.GatePieces}, gravel cells drawn {_main.Ground.GravelCells}");
        if (_main.Edits.GatePieces < 1 || _main.Ground.GravelCells != laid) { Quit(1, "gate or gravel not drawn"); return; }
        sim.StepTicks(2 * _main.Params.TicksPerDay);
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.FramePlay(instant: true);
        await Frames(6);
        Check(await Snap("hamlet-paths", true), "hamlet-paths");
        _main.Rig.LookAt(new Vector3(fx * 2f + 1, 0, fy * 2f + 3), 60f);
        await Frames(4);
        Check(await Snap("hamlet-paths-close", true), "hamlet-paths-close");
        // The gravel tool brushing on along the same trail, held before release: the ghost strip and the preview's readout
        // with its gravel note by the pointer.
        var more = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 8 && cur >= 0; i++)
        {
            more.Add(cur);
            int next = -1; float nw = 10;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int v = cur + dy * n + dx;
                    if (v < 0 || v >= n * n || strip.Contains(v) || more.Contains(v) || !w.CanGravel(v)) continue;
                    if (w.Wear[v] > nw) { nw = w.Wear[v]; next = v; }
                }
            cur = next;
        }
        if (more.Count < 2) { Quit(1, "no trail to brush on along"); return; }
        int m0 = more[0], m1 = more[more.Count - 1];
        _main.Rig.LookAt(new Vector3((m0 % n + m1 % n) + 1f, 0, (m0 / n + m1 / n) + 1f), 80f);
        await Frames(3);
        Key(Godot.Key.G);
        await Frames(1);
        var a = Cell(m0 % n, m0 / n);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        await Frames(1);
        foreach (int c in more)
        {
            var p = Cell(c % n, c / n);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left });
            await Frames(1);
        }
        for (int i = 0; i < 60 && !_main.Tools.Previewer.Settled; i++) await Frames(1);
        await Frames(6);
        GD.Print($"SCREENSHOT hamlet-gravel-preview: brush {_main.Tools.BrushCells} cells, '{_main.Tools.Previewer.Text}'");
        Check(await Snap("hamlet-gravel-preview", true), "hamlet-gravel-preview");
        Key(Godot.Key.Escape);
        await Frames(2);
    }
}
