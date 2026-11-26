using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Green pass: the small things a town dresses itself with as it prospers. They cost nothing and do nothing.
/// The names match the art exactly.</summary>
public enum TouchKind : byte { Woodpile = 0, WashingLine = 1, FlowerBed = 2, HenCoop = 3, Bench = 4, Beehives = 5, Haystack = 6, AppleTree = 7, Handcart = 8, Signpost = 9 }

/// <summary>One reserved spot for a touch (one map cell). Plot: the first-age plot it belongs to (-1 if it belongs to a
/// river plot); RiverPlot: the second-age plot (-1 if first age). X, Y: the cell centre. Heading: radians, faces its plot.</summary>
public readonly struct TouchSpot
{
    public readonly TouchKind Kind;
    public readonly double X, Y, Heading;
    public readonly int Plot, RiverPlot, CellX, CellY;
    public TouchSpot(TouchKind k, int cx, int cy, double heading, int plot, int riverPlot)
    { Kind = k; CellX = cx; CellY = cy; X = cx + 0.5; Y = cy + 0.5; Heading = heading; Plot = plot; RiverPlot = riverPlot; }
}

/// <summary>Places the reserved spots of one seed: beside the plots of both ages, clear of roads, lanes, crossings, plots,
/// doors, water and each other (no two in touching cells). Deterministic from the plan.</summary>
public static class TownTouchPlanner
{
    private static readonly TouchKind[] CottageKinds = { TouchKind.Woodpile, TouchKind.WashingLine, TouchKind.FlowerBed, TouchKind.HenCoop, TouchKind.AppleTree };

    private static TouchKind KindFor(PlotKind k, int i) => k switch
    {
        PlotKind.Cottage => CottageKinds[i % CottageKinds.Length],
        PlotKind.Well => i % 2 == 0 ? TouchKind.Bench : TouchKind.FlowerBed,
        PlotKind.Field => i % 2 == 0 ? TouchKind.Haystack : TouchKind.Beehives,
        PlotKind.Mill => i % 2 == 0 ? TouchKind.Handcart : TouchKind.Woodpile,
        PlotKind.Market => i % 2 == 0 ? TouchKind.Signpost : TouchKind.Handcart,
        _ => i % 2 == 0 ? TouchKind.Bench : TouchKind.AppleTree,
    };

    private static TouchKind KindFor(RiverPlotKind k, int i) => k switch
    {
        RiverPlotKind.Landing => i % 2 == 0 ? TouchKind.Handcart : TouchKind.Signpost,
        RiverPlotKind.Weaver => i % 2 == 0 ? TouchKind.WashingLine : TouchKind.FlowerBed,
        RiverPlotKind.Pasture => i % 2 == 0 ? TouchKind.Haystack : TouchKind.AppleTree,
        _ => CottageKinds[(i + 2) % CottageKinds.Length],
    };

    /// <summary>Every spot of the seed, in the order they are dressed: the first spot of each plot (by level, then id), then
    /// the second; first-age plots before river plots of the same round.</summary>
    public static TouchSpot[] Build(TownPlan tp)
    {
        var rp = TownRiverPlanner.Build(tp);
        int n = tp.Map.Size;
        var blocked = new byte[n * n];
        void Block(int x, int y) { if (x >= 0 && y >= 0 && x < n && y < n) blocked[y * n + x] = 1; }
        void Line(double[] px, double[] py)
        {
            for (int i = 0; i + 1 < px.Length; i++)
            {
                double dx = px[i + 1] - px[i], dy = py[i + 1] - py[i];
                int steps = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) * 16) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    double x = px[i] + dx * s / steps, y = py[i] + dy * s / steps;
                    // A point on a cell edge blocks both cells.
                    for (int e = 0; e < 4; e++) Block((int)Math.Floor(x + ((e & 1) == 0 ? -0.01 : 0.01)), (int)Math.Floor(y + ((e & 2) == 0 ? -0.01 : 0.01)));
                }
            }
            if (px.Length == 1) Block((int)Math.Floor(px[0]), (int)Math.Floor(py[0]));
        }
        for (int i = 0; i < n * n; i++) if (tp.Map.Water[i] != 0 || tp.Map.Ford[i] != 0 || tp.Map.Footprint[i] != 0) blocked[i] = 1;
        foreach (var p in tp.Plots) { for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) Block(x, y); Block(p.DoorX, p.DoorY); }
        foreach (var r in tp.Roads) { for (int i = 0; i < r.Xs.Length; i++) Block(r.Xs[i], r.Ys[i]); Line(r.Px, r.Py); }
        foreach (var nd in tp.Nodes) Block(nd.X, nd.Y);
        foreach (var p in rp.Plots) for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) Block(x, y);
        foreach (var l in rp.Links) Line(l.Px, l.Py);
        for (int i = 0; i < rp.Px.Length; i++) Block((int)Math.Floor(rp.Px[i]), (int)Math.Floor(rp.Py[i]));
        // Keep a cell of grass between a touch and water (the bank), and off the boats' course rows.
        var near = new byte[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (tp.Map.Water[y * n + x] == 0) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int xx = x + dx, yy = y + dy; if (xx >= 0 && yy >= 0 && xx < n && yy < n) near[yy * n + xx] = 1; }
            }
        var taken = new byte[n * n];
        var spots = new List<TouchSpot>();
        // Owners in dressing order: first-age plots by level then id, then river plots by level then id.
        var owners = new List<(int lvl, int plot, int rplot, int x, int y, int w, int h, int kind)>();
        foreach (var p in tp.Plots) owners.Add((p.Level, p.Id, -1, p.X, p.Y, p.W, p.H, (int)p.Kind));
        foreach (var p in rp.Plots) owners.Add((p.Level, -1, p.Id, p.X, p.Y, p.W, p.H, (int)p.Kind));
        owners.Sort((a, b) => a.lvl != b.lvl ? a.lvl.CompareTo(b.lvl) : a.rplot != b.rplot ? a.rplot.CompareTo(b.rplot) : a.plot.CompareTo(b.plot));
        for (int round = 0; round < TownRules.TouchesPerPlot; round++)
            foreach (var o in owners)
            {
                if (spots.Count >= TownRules.TouchSpotMax) break;
                bool found = false;
                // Rings 1 and 2 around the plot; start each ring at a corner that depends on the seed, plot and round.
                for (int ring = 1; ring <= 3 && !found; ring++)
                {
                    int x0 = o.x - ring, y0 = o.y - ring, x1 = o.x + o.w - 1 + ring, y1 = o.y + o.h - 1 + ring;
                    var cells = new List<(int x, int y)>();
                    for (int x = x0; x <= x1; x++) cells.Add((x, y0));
                    for (int y = y0 + 1; y <= y1; y++) cells.Add((x1, y));
                    for (int x = x1 - 1; x >= x0; x--) cells.Add((x, y1));
                    for (int y = y1 - 1; y > y0; y--) cells.Add((x0, y));
                    int start = (int)(((uint)(tp.Seed * 7919 + (o.plot + 1) * 104729 + (o.rplot + 1) * 1299709 + round * 15485863) & 0x7fffffff) % (uint)cells.Count);
                    for (int k = 0; k < cells.Count && !found; k++)
                    {
                        var (cx, cy) = cells[(start + k) % cells.Count];
                        if (cx < 1 || cy < 1 || cx >= n - 1 || cy >= n - 1) continue;
                        if (blocked[cy * n + cx] != 0 || near[cy * n + cx] != 0) continue;
                        bool clear = true;
                        for (int dy = -1; dy <= 1 && clear; dy++) for (int dx = -1; dx <= 1; dx++) if (taken[(cy + dy) * n + cx + dx] != 0) { clear = false; break; }
                        if (!clear) continue;
                        taken[cy * n + cx] = 1;
                        double hx = o.x + o.w / 2.0 - (cx + 0.5), hy = o.y + o.h / 2.0 - (cy + 0.5);
                        var kind = o.plot >= 0 ? KindFor((PlotKind)o.kind, round + o.plot) : KindFor((RiverPlotKind)o.kind, round + o.rplot);
                        spots.Add(new TouchSpot(kind, cx, cy, Math.Atan2(hy, hx), o.plot, o.rplot));
                        found = true;
                    }
                }
            }
        return spots.ToArray();
    }
}

/// <summary>What the game reads of one touch that has appeared.</summary>
public readonly struct TouchView
{
    public readonly TouchKind Kind;
    public readonly double X, Y, Heading;
    public readonly int Plot, RiverPlot;
    public TouchView(in TouchSpot s) { Kind = s.Kind; X = s.X; Y = s.Y; Heading = s.Heading; Plot = s.Plot; RiverPlot = s.RiverPlot; }
}

public sealed partial class Town
{
    private TouchSpot[]? _spots;
    /// <summary>Every reserved spot of this seed (the same in both ages; touches appear on them one by one).</summary>
    public TouchSpot[] TouchSpots => _spots ??= Plan.TouchSpots ??= TownTouchPlanner.Build(Plan);
    /// <summary>Touches that have appeared so far.</summary>
    public int TouchCount => S.TouchCount;
    /// <summary>The i-th touch to appear (0 .. TouchCount - 1).</summary>
    public TouchView Touch(int i) => new(TouchSpots[S.TouchOrder[i]]);
    /// <summary>Coin earned in all at which the next touch appears (when a spot is free beside a plot the town has).</summary>
    public double NextTouchAtCoin => S.TouchNextCoin;
    private static readonly string[] TouchNames = Enum.GetNames(typeof(TouchKind));

    private bool SpotReady(in TouchSpot s) =>
        s.Plot >= 0 ? S.PlotGiven[s.Plot] : S.River != null && S.River.PlotGiven.Length > s.RiverPlot && S.River.PlotGiven[s.RiverPlot];

    /// <summary>Once a second: when the coin earned passes the beat, the next free spot beside a plot the town has gets its touch,
    /// and the next beat is TownRules.TouchMinutes of the town's income over the last beat (TouchMinutesRiver in the second age) (at least TouchRateMin a minute) further on.</summary>
    private void StepTouches()
    {
        if (S.TouchNextCoin <= 0) { S.TouchNextCoin = S.CoinEarned + TownRules.TouchFirstCoin; S.TouchLastCoin = S.CoinEarned; S.TouchLastTime = Time; }
        // The beat is the coin earned; a backstop (TownRules.TouchMaxGap seconds since the last touch) keeps a quiet town dressing.
        if ((S.CoinEarned < S.TouchNextCoin && Time - S.TouchLastTime < TownRules.TouchMaxGap) || S.TouchCount >= S.TouchOrder.Length) return;
        var sp = TouchSpots;
        for (int i = 0; i < sp.Length; i++)
        {
            bool used = false;
            for (int j = 0; j < S.TouchCount; j++) if (S.TouchOrder[j] == i) { used = true; break; }
            if (used || !SpotReady(sp[i])) continue;
            S.TouchOrder[S.TouchCount++] = i;
            // The town's income over the last beat (smoother than the last minute, which jumps when a boat pays). The second age
            // has its own, quicker beat: its income comes in bursts (boats), and the first age needs fewer touches.
            // A boat's lump of coin inflates one beat's average: take the lower of the last two beats and of the last minute.
            double last = (S.CoinEarned - S.TouchLastCoin) * 60 / Math.Max(1, Time - S.TouchLastTime);
            double rate = Math.Min(last, CoinPerMinute);
            if (S.TouchPrevRate > 0) rate = Math.Min(rate, S.TouchPrevRate);
            S.TouchPrevRate = last;
            double minutes = S.River != null ? TownRules.TouchMinutesRiver : TownRules.TouchMinutes;
            S.TouchNextCoin = S.CoinEarned + minutes * Math.Max(TownRules.TouchRateMin, rate);
            S.TouchLastCoin = S.CoinEarned; S.TouchLastTime = Time;
            Emit(new TownEvent { Kind = TownEventKind.TouchAdded, Index = S.TouchCount - 1, Level = (int)sp[i].Kind, Plot = sp[i].Plot, Amount = sp[i].RiverPlot, X = sp[i].X, Y = sp[i].Y, Value = sp[i].Heading, Text = TouchNames[(int)sp[i].Kind] });
            return;
        }
    }
}
