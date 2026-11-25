using System;
using System.Collections.Generic;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// What a tool would touch, asked of the world without changing it: why no footbridge fits at a cell (in words), and
/// the fence and bridge cells the eraser's 3 x 3 brush would remove. Plain C#, no allocation (the caller's lists).
/// </summary>
public static class ToolTargets
{
    /// <summary>Why no footbridge fits at a cell, in words (mirrors World.BridgeCellsAt's checks; read-only).</summary>
    public static string BridgeReason(World w, int cx, int cy)
    {
        int n = w.Grid.Width;
        if (cy < 0 || cy >= n) return "Point at the river to lay a footbridge.";
        int rx = (int)MathF.Floor(w.RiverX[cy]);
        if (Math.Abs(cx - rx) > w.Map.RiverHalfW + 4) return "A footbridge goes across the river: point at the water.";
        if (Math.Abs(cy - w.Map.FordY) <= 2) return FordNote(w, "Too close to the ford: villagers already cross here");
        for (int x = Math.Max(0, rx - 8); x <= Math.Min(n - 1, rx + 8); x++)
            if (w.BridgeAt[cy * n + x] >= 0) return "There is already a footbridge here.";
        return "No footbridge fits here: both ends need open ground on the bank.";
    }

    /// <summary>
    /// Why a crossing of type t cannot be built for a pointer at (x, y) in cells, in words, or null when it can. Asks the
    /// simulation, but never so that it logs a "cannot afford" event while the player is only pointing: when the purse
    /// is short, the spot is checked without the coin and the coin reason is worded here, in the simulation's words.
    /// </summary>
    public static string? CrossingReason(World w, CrossingType t, double x, double y)
    {
        var r = w.River;
        if (r == null) return "The river rules are off.";
        double price = r.Rules.Price(t);
        if (r.Money >= price - 1e-9)
        {
            var res = w.CheckCrossing(t, x, y);
            return res.Ok ? null : Spot(w, res.Reason);
        }
        if (w.CrossingWhatIf(t, x, y) == null) return Spot(w, w.CheckCrossing(t, x, y).Reason); // the spot's reason comes first
        return $"Not enough coin: the {World.CrossingName(t)} costs {RiverFeed.Coin(price)}, you have {RiverFeed.Coin(r.Money)}.";
    }

    /// <summary>A spot's reason in words, with the flood's news when it is about the ford.</summary>
    private static string Spot(World w, string reason) => reason.Contains("ford") ? FordNote(w, reason) : reason + ".";

    /// <summary>"Too close to the ford." and, while the spring flood has closed it, what that means for now.</summary>
    public static string FordNote(World w, string reason)
    {
        var r = w.River;
        if (r == null || !r.Flooded) return reason + ".";
        double left = Math.Max(0, r.FloodEndsAt - w.Time) / w.Params.DaySimSec;
        string when = left < 1 ? "later today" : left < 2 ? "tomorrow" : $"in {Math.Ceiling(left):0} days";
        return $"{reason}. The ford is closed by the spring flood: it opens again {when}, when the water goes down.";
    }

    /// <summary>What erasing these crossing cells costs, in words ("Erase: taking down the ferry costs 20c.").</summary>
    public static string EraseCost(World w, System.Collections.Generic.List<int> cells)
    {
        var r = w.River!;
        double cost = 0;
        string what = "";
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            if (x.Type == CrossingType.Ford || x.Cells.Count == 0 || !cells.Contains(x.Cells[0])) continue;
            cost += w.EraseCost(x.Type);
            what = what.Length == 0 ? World.CrossingName(x.Type) : "crossings";
        }
        if (what.Length == 0) return "Erase: click to remove the bridge.";
        if (r.Money - cost < r.Rules.DebtLimit - 1e-9)
            return $"Not enough coin: taking down the {what} costs {RiverFeed.Coin(cost)}, you have {RiverFeed.Coin(r.Money)} " +
                   $"and the purse may not go below {RiverFeed.Coin(r.Rules.DebtLimit)}.";
        return $"Erase: taking down the {what} costs {RiverFeed.Coin(cost)} (no refund). Click to remove it.";
    }

    /// <summary>Fence (with its gates) and gravel cells in the 3 x 3 round a cell, and every cell of any bridge touched (the toy's brush).</summary>
    public static void EraseTargets(World w, int x, int y, List<int> fenceT, List<int> bridgeT, List<int> gravelT)
    {
        int n = w.Grid.Width;
        fenceT.Clear(); bridgeT.Clear(); gravelT.Clear();
        var fence = w.Fence;
        var gravel = w.Gravel;
        var bridgeAt = w.BridgeAt;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                int c = yy * n + xx;
                if (fence[c] != 0) fenceT.Add(c);
                if (gravel[c] != 0) gravelT.Add(c);
                // A ferry is no bridge: its water cells are found through the river's crossing map.
                if (w.River != null && bridgeAt[c] < 0 && w.CrossingAt(c) >= 0 && !bridgeT.Contains(c))
                {
                    var xs = w.River.Crossings;
                    int id = w.CrossingAt(c);
                    for (int i = 0; i < xs.Count; i++)
                        if (xs[i].Id == id)
                            foreach (int fc in xs[i].Cells) if (!bridgeT.Contains(fc)) bridgeT.Add(fc);
                }
                int b = bridgeAt[c];
                if (b < 0 || bridgeT.Contains(c)) continue;
                foreach (var br in w.Bridges)
                    if (br.Id == b)
                        foreach (int bc in br.Cells) if (!bridgeT.Contains(bc)) bridgeT.Add(bc);
            }
    }
}
