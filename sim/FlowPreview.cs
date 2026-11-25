using System;
using System.Collections.Generic;
using System.Globalization;

namespace TideToll.Sim;

/// <summary>A hypothetical edit. Cells are grid indices; null means none.</summary>
public readonly struct WhatIf
{
    /// <summary>New fence cells.</summary>
    public int[]? Block { get; init; }
    /// <summary>Fence cells removed.</summary>
    public int[]? Open { get; init; }
    /// <summary>Water cells that become a footbridge.</summary>
    public int[]? Bridge { get; init; }
    /// <summary>Footbridge cells that become water again.</summary>
    public int[]? CloseBridge { get; init; }
    /// <summary>New gravel.</summary>
    public int[]? Gravel { get; init; }
    /// <summary>Gravel removed.</summary>
    public int[]? Ungravel { get; init; }
    /// <summary>Fence cells that become one gate (two cells: a stair-corner gate, half the delay each).</summary>
    public int[]? Gate { get; init; }
    /// <summary>Gates that become plain fence.</summary>
    public int[]? Ungate { get; init; }
    /// <summary>A river crossing that would be built (river rules only); see <see cref="World.CrossingWhatIf"/>.</summary>
    public CrossingWhatIf? Crossing { get; init; }
}

/// <summary>
/// A crossing in a what-if: its water cells get the same per-cell cost as the built crossing, with no learned wait
/// yet (a ferry's expected load, headway and ride; a bridge's deck). Row and clearance give the boats it would hold.
/// </summary>
public readonly struct CrossingWhatIf
{
    public CrossingType Type { get; init; }
    public int Row { get; init; }
    public int[] Cells { get; init; }
}

/// <summary>One (house, destination, group of walkers) whose typical route the change would alter.</summary>
public sealed class RouteChange
{
    public int Home { get; init; }
    public int Dest { get; init; }
    /// <summary>
    /// Trips out per day that take this route: the pair's trips times the group's share (all of the pair's trips when
    /// the change cuts it off).
    /// </summary>
    public double Rate { get; init; }
    /// <summary>The group: the house's explorers, or its other walkers (preview version 2; always false before).</summary>
    public bool Explorers { get; init; }
    /// <summary>Whether these trips are walked on the frugal view after the change.</summary>
    public bool Frugal { get; init; }
    /// <summary>Typical route now, x,y pairs in cell units.</summary>
    public float[] OldPts { get; init; } = Array.Empty<float>();
    /// <summary>Typical route after the change (cut short where it is blocked).</summary>
    public float[] NewPts { get; init; } = Array.Empty<float>();
    public bool Reachable { get; init; }
    /// <summary>New route cost / old route cost, in the after-change walker's terms (+Infinity when blocked).</summary>
    public double CostRatio { get; init; }
}

/// <summary>The result of a flow preview (the toy's FlowPreview).</summary>
public sealed class FlowPreview
{
    /// <summary>The biggest changed pairs, at most maxPairs, busiest first.</summary>
    public List<RouteChange> Changes { get; } = new();
    /// <summary>Changed pairs left out.</summary>
    public int More { get; init; }
    /// <summary>Trips per day over all changed routes.</summary>
    public double ReroutedPerDay { get; init; }
    /// <summary>Rate-weighted mean of CostRatio over the rerouted trips that still arrive (1 if none).</summary>
    public double MeanCostRatio { get; init; } = 1;
    /// <summary>
    /// For a gravel strip that changes no route: Far (no cell within 6 cells of a trail), On (most of it lies on a route
    /// walkers use now), Near (a cell within 3 cells of one); None otherwise.
    /// </summary>
    public GravelNote Note { get; init; }
    /// <summary>
    /// The readout's wording: true (preview version 2, the finished toy) gives the mean cost change of the rerouted
    /// trips, "(+8% longer on average)"; false (round 2) the busiest changed route's, "(+8% longer)".
    /// </summary>
    public bool OnAverage { get; init; }
    /// <summary>Destinations cut off, and for how many houses, in the order first found.</summary>
    public List<(int Dest, int Houses)> Blocked { get; } = new();
    /// <summary>World.Version when the computation started.</summary>
    public int Version { get; init; }
    /// <summary>River rules: boats a day that the previewed crossing would turn back (masts above its clearance), and those that would need a drawbridge raised.</summary>
    public double BoatsBlockedPerDay { get; init; }
    public double BoatsNeedRaisePerDay { get; init; }
}


/// <summary>What a gravel strip that changes no route is said to do (the toy's FlowPreview.note).</summary>
public enum GravelNote : byte { None = 0, Far, On, Near }

// The readout and the route helpers of the flow preview (preview.ts: describePreview, maxDeviation, gravelNote).
public sealed partial class FlowPredictor
{
    private const int FarCells = 6;  // a gravel strip with no cell within this many cells of a trail is "too far from any route"
    private const int NearCells = 3; // ... and one within this many cells of a used route "may draw walkers"
    private const double UsedFlow = 3; // a cell is on a used route if it is trail (wear 25) or has this many recent footsteps

    /// <summary>The readout of each gravel note (the toy's GRAVEL_NOTES).</summary>
    public static string GravelNoteText(GravelNote note) => note switch
    {
        GravelNote.Far => "Too far from any route to change anything",
        GravelNote.On => "On a route walkers already use: it keeps the trail from growing back",
        GravelNote.Near => "May draw walkers from nearby trails",
        _ => "",
    };

    /// <summary>The largest distance from any point of <paramref name="a"/> to the nearest point of <paramref name="b"/>, in cells.</summary>
    public static double MaxDeviation(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double worst = 0;
        for (int i = 0; i < a.Length; i += 2)
        {
            double best = double.PositiveInfinity;
            for (int j = 0; j < b.Length; j += 2)
            {
                double dx = (double)a[i] - b[j], dy = (double)a[i + 1] - b[j + 1];
                double d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            if (best > worst) worst = best;
        }
        return Math.Sqrt(worst);
    }

    /// <summary>The note for a gravel strip that changes no route (see <see cref="FlowPreview.Note"/>).</summary>
    internal static GravelNote GravelNoteFor(World w, int[] cells)
    {
        int n = w.Grid.Width;
        double trail = w.Params.ThresholdTrail;
        var wear = w.Wear;
        bool far = true;
        foreach (int c in cells)
        {
            int x = c % n, y = c / n;
            for (int yy = y - FarCells; yy <= y + FarCells && far; yy++)
                for (int xx = x - FarCells; xx <= x + FarCells && far; xx++)
                    if (xx >= 0 && yy >= 0 && xx < n && yy < n && wear[yy * n + xx] >= trail) far = false;
            if (!far) break;
        }
        if (far) return GravelNote.Far;
        int on = 0;
        bool near = false;
        foreach (int c in cells)
        {
            if (Used(w, c)) { on++; continue; }
            int x = c % n, y = c / n;
            for (int yy = y - NearCells; yy <= y + NearCells && !near; yy++)
                for (int xx = x - NearCells; xx <= x + NearCells && !near; xx++)
                    if (xx >= 0 && yy >= 0 && xx < n && yy < n && Used(w, yy * n + xx)) near = true;
        }
        return on * 2 >= cells.Length ? GravelNote.On : near || on > 0 ? GravelNote.Near : GravelNote.None;
    }

    /// <summary>Whether walkers use cell c now: trail, or recent footsteps.</summary>
    private static bool Used(World w, int c) => w.Wear[c] >= w.Params.ThresholdTrail || w.Flow[c] >= UsedFlow;

    /// <summary>
    /// One-line readout, e.g. "Reroutes about 40 walkers a day (+8% longer on average)" (version 2: the rate-weighted
    /// mean cost change of the rerouted trips; version 1: "(+8% longer)", the busiest changed route's), then the
    /// river's boat lines.
    /// </summary>
    public static string Describe(FlowPreview p, Func<int, string> name)
    {
        string line = DescribeRoutes(p, name);
        if (p.BoatsBlockedPerDay > 0.05) line += $". Blocks barges: about {Boats(p.BoatsBlockedPerDay)} a day will turn back";
        if (p.BoatsNeedRaisePerDay > 0.05) line += $". Barges need it raised: about {Boats(p.BoatsNeedRaisePerDay)} a day";
        return line;
    }

    private static string Boats(double n)
    {
        double r = Math.Max(1, JsMath.Round(n));
        return Num(r) + (r == 1 ? " boat" : " boats");
    }

    private static string DescribeRoutes(FlowPreview p, Func<int, string> name)
    {
        var parts = new List<string>();
        foreach (var (dest, houses) in p.Blocked)
            parts.Add($"Blocks the {name(dest)} for {houses} house{(houses == 1 ? "" : "s")}");
        double n = p.ReroutedPerDay;
        if (n < 0.5) return parts.Count > 0 ? parts[0] : p.Note != GravelNote.None ? GravelNoteText(p.Note) : "No change to routes";
        string shown = n < 5
            ? Num(Math.Max(1, JsMath.Round(n)))
            : "about " + Num(Math.Max(5, JsMath.Round(n / 5) * 5));
        string line = $"Reroutes {shown} walker{(shown == "1" ? "" : "s")} a day";
        if (p.OnAverage)
        {
            if (Math.Abs(p.MeanCostRatio - 1) >= 0.02)
            {
                double pct = JsMath.Round((p.MeanCostRatio - 1) * 100);
                line += pct > 0 ? $" (+{Num(pct)}% longer on average)" : $" ({Num(-pct)}% shorter on average)";
            }
        }
        else
        {
            RouteChange? big = p.Changes.Find(c => c.Reachable);
            if (big != null && Math.Abs(big.CostRatio - 1) >= 0.05)
            {
                double pct = JsMath.Round((big.CostRatio - 1) * 100);
                line += pct > 0 ? $" (+{Num(pct)}% longer)" : $" ({Num(-pct)}% shorter)";
            }
        }
        parts.Add(line);
        return string.Join(". ", parts);
    }

    private static string Num(double v) => v.ToString("0", CultureInfo.InvariantCulture);
}
