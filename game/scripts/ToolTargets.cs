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
        if (Math.Abs(cy - w.Map.FordY) <= 2) return "Too close to the ford: villagers already cross here.";
        for (int x = Math.Max(0, rx - 8); x <= Math.Min(n - 1, rx + 8); x++)
            if (w.BridgeAt[cy * n + x] >= 0) return "There is already a footbridge here.";
        return "No footbridge fits here: both ends need open ground on the bank.";
    }

    /// <summary>Fence cells in the 3 x 3 round a cell, and every cell of any bridge touched (the toy's brush).</summary>
    public static void EraseTargets(World w, int x, int y, List<int> fenceT, List<int> bridgeT)
    {
        int n = w.Grid.Width;
        fenceT.Clear(); bridgeT.Clear();
        var fence = w.Fence;
        var bridgeAt = w.BridgeAt;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                int c = yy * n + xx;
                if (fence[c] != 0) fenceT.Add(c);
                int b = bridgeAt[c];
                if (b < 0 || bridgeT.Contains(c)) continue;
                foreach (var br in w.Bridges)
                    if (br.Id == b)
                        foreach (int bc in br.Cells) if (!bridgeT.Contains(bc)) bridgeT.Add(bc);
            }
    }
}
