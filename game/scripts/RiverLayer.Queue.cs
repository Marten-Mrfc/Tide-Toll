using System;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The river layer's spacing of boats along the river: queues drawn nose to tail, clamped short of crossings and moored
/// boats (rafted up in a side lane when there is no room), and the nearest crossing either side of the landing.
/// </summary>
public partial class RiverLayer
{
    /// <summary>
    /// The simulation queues boats 2 rows apart, as points; a barge is over 6 rows long (more at overview). Boats waiting
    /// in a queue are drawn nose to tail instead (each pushed south of the one ahead by its length), so a queue reads as
    /// a queue, but never over a crossing or a moored boat further south: a boat that would reach one is held just short
    /// of it and drawn rafted up beside the boat ahead (a side lane) instead. Allocation-free.
    /// </summary>
    private void SpaceQueue(ReadOnlySpan<Boat> boats, int count, float cell, float sc)
    {
        int m = 0;
        Clamped = 0;
        for (int i = 0; i < count; i++)
        {
            _lane[i] = 0;
            if (boats[i].State == BoatState.Waiting) _order[m++] = i;
        }
        // Insertion sort by centre, north first (a handful of boats).
        for (int a = 1; a < m; a++)
        {
            int v = _order[a], j = a - 1;
            while (j >= 0 && _cy[_order[j]] > _cy[v]) { _order[j + 1] = _order[j]; j--; }
            _order[j + 1] = v;
        }
        var r = _sim.World.River!;
        if (m > 0) ClearOfMoored(boats, count, _order[0], cell, sc);
        for (int a = 1; a < m; a++)
        {
            // Behind the last boat in the middle lane (a rafted boat lies beside the queue, not in it).
            int p = _order[a - 1], q = _order[a];
            for (int k = a - 1; k >= 0; k--) if (_lane[_order[k]] == 0) { p = _order[k]; break; }
            ClearOfMoored(boats, count, q, cell, sc);
            float hq = Half(boats[q], sc);
            float half = Half(boats[p], sc) + hq;
            float want = _cy[p] + (half + 1f) / cell;
            if (_cy[q] >= want) continue;
            // The first obstacle south of where the simulation has this boat: a crossing, or a moored boat.
            float limit = SouthLimit(r, boats, count, _cy[q] - hq / cell, cell);
            float most = limit - (hq + 0.6f) / cell;
            if (want <= most) { _cy[q] = want; continue; }
            _cy[q] = MathF.Max(_cy[q], most);
            RaftBeside(boats, q, a, cell, sc);
            Clamped++;
        }
        QueueOverlaps = CountOverlaps(boats, m, cell, sc);
    }

    /// <summary>Waiting boats drawn over one another after the last layout (same lane and their hulls' lengths meet; tests: 0).</summary>
    public int QueueOverlaps { get; private set; }

    /// <summary>
    /// A boat with no room behind the queue lies rafted up in a side lane (a full beam out from the middle, so it never
    /// lies over the boats in the middle). Of the two lanes it takes the one where it needs to move least to clear every
    /// boat already lying in it (moving north, never south over the crossing or berth it was held short of).
    /// </summary>
    private void RaftBeside(ReadOnlySpan<Boat> boats, int q, int before, float cell, float sc)
    {
        float best = float.MaxValue, bestCy = _cy[q], bestLane = 1f;
        float hq = Half(boats[q], sc) / cell;
        for (int li = 0; li < 2; li++)
        {
            float lane = li == 0 ? (_lane[_order[before - 1]] > 0 ? -1f : 1f) : (_lane[_order[before - 1]] > 0 ? 1f : -1f);
            float cy = _cy[q];
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                for (int k = 0; k < before; k++)
                {
                    int o = _order[k];
                    if (_lane[o] != lane) continue;
                    float gap = Half(boats[o], sc) / cell + hq + 0.3f / cell;
                    if (MathF.Abs(_cy[o] - cy) < gap) { cy = _cy[o] - gap; moved = true; }
                }
                if (!moved) break;
            }
            float shift = _cy[q] - cy;
            if (shift < best) { best = shift; bestCy = cy; bestLane = lane; }
        }
        _cy[q] = bestCy;
        _lane[q] = bestLane;
    }

    private int CountOverlaps(ReadOnlySpan<Boat> boats, int m, float cell, float sc)
    {
        int n = 0;
        for (int a = 0; a < m; a++)
            for (int b = a + 1; b < m; b++)
            {
                int i = _order[a], j = _order[b];
                if (_lane[i] != _lane[j]) continue;
                float gap = (Half(boats[i], sc) + Half(boats[j], sc)) / cell;
                if (MathF.Abs(_cy[i] - _cy[j]) < gap - 0.05f) n++;
            }
        return n;
    }

    /// <summary>A boat waiting for a berth lies south of the moored boats, never over them.</summary>
    private void ClearOfMoored(ReadOnlySpan<Boat> boats, int count, int q, float cell, float sc)
    {
        float hq = Half(boats[q], sc) / cell;
        for (int i = 0; i < count; i++)
        {
            if (boats[i].State != BoatState.Mooring || _cy[i] > _cy[q]) continue;
            float south = _cy[i] + Half(boats[i], _bs[i]) / cell + 0.4f;
            if (_cy[q] - hq < south) { _cy[q] = south + hq; Clamped++; }
        }
    }

    /// <summary>Rows from <paramref name="row"/>'s centre to the nearest crossing (not the ford) on one side (-1 north, +1 south); 99 if none.</summary>
    private static float NearestCrossing(RiverView r, int row, float side)
    {
        float best = 99f;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            if (xs[i].Type == CrossingType.Ford) continue;
            float d = (xs[i].Row - row) * side;
            if (d > 0 && d < best) best = d;
        }
        return best;
    }

    private static float Half(in Boat b, float sc) => (b.Kind == BoatKind.Barge ? BargeBow : RowBow) * sc;

    /// <summary>The northern edge (rows) of the nearest crossing deck or moored hull south of row <paramref name="from"/>.</summary>
    private float SouthLimit(RiverView r, ReadOnlySpan<Boat> boats, int count, float from, float cell)
    {
        float best = float.MaxValue;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            if (xs[i].Type == CrossingType.Ford) continue;
            float north = xs[i].Row - 0.6f;
            if (north > from && north < best) best = north;
        }
        for (int i = 0; i < count; i++)
        {
            if (boats[i].State != BoatState.Mooring) continue;
            float north = _cy[i] - Half(boats[i], _bs[i]) / cell;
            if (north > from && north < best) best = north;
        }
        return best;
    }
}
