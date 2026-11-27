using System;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>n7: round 6's pairwise spread, kept only so the pictures can show a busy road before and after the bodies pass
/// (<see cref="OldSpread"/>, off in the game; never on in a check).</summary>
public partial class TownVillagers
{
    /// <summary>Pictures only: draw with round 6's spread instead of the bodies pass.</summary>
    public static bool OldSpread;

    /// <summary>
    /// Round 6 (W3): people never drawn through each other. Pairs of villagers closer than the personal spacing are
    /// pushed apart (drawing only; the simulation is untouched), and walkers keep a small lane of their own across the
    /// road's width by id, so a crowd reads as a crowd. Offsets are clamped to 0.6 of a cell and eased.
    /// </summary>
    private void SpreadRound6(Town t, int n, float dt)
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

}
