using System;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>Round 6 (W3): the drawn-only spacing of villagers (moved out of TownVillagers.cs in n5, unchanged but for the line).</summary>
public partial class TownVillagers
{
    /// <summary>
    /// Round 6 (W3): people never drawn through each other. Pairs of villagers closer than the personal spacing are
    /// pushed apart (drawing only; the simulation is untouched), and walkers keep a small lane of their own across the
    /// road's width by id, so a crowd reads as a crowd. Offsets are clamped to 0.6 of a cell and eased.
    /// </summary>
    private void Spread(Town t, int n, float dt)
    {
        float r = Personal * WalkerScale / Cell, cap = 0.6f;
        float minGap = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            float wx = 0, wy = 0;
            if (v.Phase == VillagerPhase.Walking)
            {
                // A lane across the road by id: -2..2 steps of 0.12 cell, across the way the villager faces.
                float lane = ((i * 7) % 5 - 2) * 0.12f;
                float fx = MathF.Sin(_yaw[i]), fz = MathF.Cos(_yaw[i]);
                wx = fz * lane; wy = -fx * lane;
            }
            for (int j = 0; j < n; j++)
            {
                if (j == i) continue;
                float dx = (_x[i] + _ox[i]) - (_x[j] + _ox[j]), dy = (_y[i] + _oy[i]) - (_y[j] + _oy[j]);
                float d2 = dx * dx + dy * dy;
                if (d2 >= r * r) continue;
                float d = MathF.Sqrt(d2);
                if (d < 1e-3f)
                {
                    // Exactly together: split them by id so the lower id stays and the higher steps aside.
                    float ang = (i > j ? i : j) * 2.39996f;
                    dx = MathF.Cos(ang) * (i > j ? 1 : -1); dy = MathF.Sin(ang) * (i > j ? 1 : -1); d = 1e-3f;
                    wx += dx * r * 0.5f; wy += dy * r * 0.5f;
                }
                else { wx += dx / d * (r - d) * 0.55f; wy += dy / d * (r - d) * 0.55f; }
            }
            float wl = MathF.Sqrt(wx * wx + wy * wy);
            if (wl > cap) { wx *= cap / wl; wy *= cap / wl; }
            // n5: those in a line and those at work keep their places (the line's own spacing); others step round them.
            if (_inLine[i] || _atWork[i]) { wx = 0; wy = 0; }
            else if (v.Phase == VillagerPhase.Queued) OutOfBuildings(t, _x[i] + wx, _y[i] + wy, ref wx, ref wy);
            float k = Math.Min(1f, dt * 8f);
            _ox[i] += (wx - _ox[i]) * k;
            _oy[i] += (wy - _oy[i]) * k;
            if (v.Phase is VillagerPhase.Queued or VillagerPhase.Working)
                for (int j = 0; j < i; j++)
                {
                    var u = t.Villager(j);
                    if (u.Phase is not (VillagerPhase.Queued or VillagerPhase.Working)) continue;
                    float dx = (_x[i] + _ox[i]) - (_x[j] + _ox[j]), dy = (_y[i] + _oy[i]) - (_y[j] + _oy[j]);
                    minGap = Math.Min(minGap, MathF.Sqrt(dx * dx + dy * dy) * Cell);
                }
        }
        MinStandingGap = minGap;
    }

    /// <summary>
    /// Round 6 (W3): a queued villager whose drawn spot falls inside a built plot's footprint (the mill, a cottage, the
    /// landing's stall) is drawn just outside its nearest edge, beside the building along the lane. Fields are walked in.
    /// </summary>
    private static void OutOfBuildings(Town t, float x, float y, ref float wx, ref float wy)
    {
        var plots = t.Plan.Plots;
        const float margin = 0.35f;
        for (int k = 0; k < plots.Length; k++)
        {
            var p = plots[k];
            if (p.Kind == PlotKind.Field || !t.PlotGiven(p.Id)) continue;
            float x0 = p.X + 0.1f, y0 = p.Y + 0.1f, x1 = p.X + p.W - 0.1f, y1 = p.Y + p.H - 0.1f;
            if (x <= x0 || x >= x1 || y <= y0 || y >= y1) continue;
            float l = x - x0, r = x1 - x, u = y - y0, d = y1 - y, m = Math.Min(Math.Min(l, r), Math.Min(u, d));
            if (m > 1.5f) continue; // deep inside: the simulation means it (a worker's spot); leave it
            if (m == l) wx -= l + margin; else if (m == r) wx += r + margin; else if (m == u) wy -= u + margin; else wy += d + margin;
            return;
        }
    }
}
