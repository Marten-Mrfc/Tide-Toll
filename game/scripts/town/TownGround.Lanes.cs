using System;
using System.Collections.Generic;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 5: the second age's lanes are drawn by the ground like the first age's roads: the cells along the lane's own
/// polyline (RiverLink.Px/Py, whatever points it has: the simulation's lanes may gain smooth curves), drawn in from the
/// near end when the lane opens, a faint trail first that wears in to firm earth over <see cref="LaneWearSeconds"/> of
/// game time. Lanes are never paved by the simulation, so they stay earth. River positions are at cell centres, road
/// points at cell corners: lane points are shifted half a cell before the same cell walk as roads.
/// Round n3 (RV): each crossing's two bank ends get a short worn way too (<see cref="Approaches"/>).
/// </summary>
public partial class TownGround
{
    /// <summary>Game seconds a new lane takes to wear in from a faint trail to firm earth.</summary>
    public const float LaneWearSeconds = 90f;
    private RiverPlan? _lanePlan;
    private int[][] _laneCells = Array.Empty<int[]>();
    private float[][] _laneDist = Array.Empty<float[]>();
    private float[] _laneReveal = Array.Empty<float>();
    private double[] _laneOpenAt = Array.Empty<double>();

    /// <summary>Lanes drawn (tests).</summary>
    public float LaneReveal(int link) => link >= 0 && link < _laneReveal.Length ? _laneReveal[link] : 0f;
    public float LaneShown(int link)
    {
        if (link < 0 || link >= _laneCells.Length || _laneCells[link].Length == 0) return 0f;
        float s = 0;
        foreach (int c in _laneCells[link]) s += _shown[c];
        return s / _laneCells[link].Length;
    }

    /// <summary>The cells a polyline in map cells passes through, 4-connected, with the distance along it of each.</summary>
    public static void CellsAlong(double[] px, double[] py, double shift, List<int> cells, List<float> dist)
    {
        cells.Clear(); dist.Clear();
        if (px.Length == 0) return;
        int ox = (int)Math.Floor(px[0] + shift + 0.5), oy = (int)Math.Floor(py[0] + shift + 0.5);
        double along = 0;
        Add(ox, oy, 0);
        for (int i = 0; i + 1 < px.Length; i++)
        {
            double ax = px[i] + shift, ay = py[i] + shift, bx = px[i + 1] + shift, by = py[i + 1] + shift;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int k = Math.Max(1, (int)Math.Ceiling(len * 10));
            for (int j = 1; j <= k; j++)
            {
                int cx = (int)Math.Floor(ax + (bx - ax) * j / k + 0.5), cy = (int)Math.Floor(ay + (by - ay) * j / k + 0.5);
                if (cx == ox && cy == oy) continue;
                float d = (float)(along + len * j / k);
                if (cx != ox && cy != oy) Add(cx, oy, d);
                Add(cx, cy, d);
                ox = cx; oy = cy;
            }
            along += len;
        }

        void Add(int x, int y, double d)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return;
            cells.Add(y * N + x); dist.Add((float)d);
        }
    }

    private void BuildLanes(RiverPlan rp)
    {
        _lanePlan = rp;
        int n = rp.Links.Length;
        _laneCells = new int[n][]; _laneDist = new float[n][]; _laneReveal = new float[n]; _laneOpenAt = new double[n];
        var cells = new List<int>(); var dist = new List<float>();
        for (int i = 0; i < n; i++)
        {
            var l = rp.Links[i];
            if (l.Kind != RiverLinkKind.Lane) { Approaches(l, cells, dist); _laneCells[i] = cells.ToArray(); _laneDist[i] = dist.ToArray(); _laneOpenAt[i] = -1; continue; }
            CellsAlong(l.Px, l.Py, -0.5, cells, dist);
            _laneCells[i] = cells.ToArray(); _laneDist[i] = dist.ToArray();
            _laneOpenAt[i] = -1;
        }
    }

    /// <summary>Round n3 (asset track RV, after the review): a crossing (ford, bridges, ferry) gets a trodden way on each
    /// bank, ApproachCells long, running away from the river from its two ends, so its stones or ramps are reached by a
    /// worn path and do not stand on bare grass. Worn like a lane once the crossing opens.</summary>
    private const double ApproachCells = 3.0;
    private static void Approaches(RiverLink l, List<int> cells, List<float> dist)
    {
        cells.Clear(); dist.Clear();
        int n = l.Px.Length;
        if (n < 2) return;
        double dx = l.Px[n - 1] - l.Px[0], dy = l.Py[n - 1] - l.Py[0];
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        dx /= len; dy /= len;
        var c2 = new List<int>(); var d2 = new List<float>();
        for (int e = 0; e < 2; e++)
        {
            double ex = e == 0 ? l.Px[0] : l.Px[n - 1], ey = e == 0 ? l.Py[0] : l.Py[n - 1];
            double sx = e == 0 ? -dx : dx, sy = e == 0 ? -dy : dy;   // away from the river
            CellsAlong(new[] { ex + sx * 0.5, ex + sx * (0.5 + ApproachCells) }, new[] { ey + sy * 0.5, ey + sy * (0.5 + ApproachCells) },
                -0.5, c2, d2);
            cells.AddRange(c2); for (int k = 0; k < c2.Count; k++) dist.Add(0f);
        }
    }

    /// <summary>Per frame: a lane that opens draws itself in. Returns whether any lane is still drawing in.</summary>
    private bool LanesFrame(Town town, double delta)
    {
        if (!town.InRiverAge || town.RiverPlan == null) { if (_lanePlan != null) { _lanePlan = null; _laneCells = Array.Empty<int[]>(); _laneReveal = Array.Empty<float>(); } return false; }
        if (!ReferenceEquals(_lanePlan, town.RiverPlan)) BuildLanes(town.RiverPlan);
        bool revealing = false;
        for (int i = 0; i < _laneCells.Length; i++)
        {
            if (_laneCells[i].Length == 0 || !town.RiverLinkOpen(i)) continue;
            if (_laneOpenAt[i] < 0)
            {
                // Open when first seen: at once and worn in on a loaded town, drawn in when it opens in play.
                bool atOnce = _drv.StepsRun == 0;
                _laneOpenAt[i] = atOnce ? town.Time - LaneWearSeconds : town.Time;
                _laneReveal[i] = atOnce ? 1f : 0.0001f;
            }
            if (_laneReveal[i] < 1f) { _laneReveal[i] = Math.Min(1f, _laneReveal[i] + (float)delta / RevealSeconds); revealing = true; }
        }
        return revealing;
    }

    /// <summary>In Upload: the lanes' wear into the target (like a road's track surface).</summary>
    private void LanesUpload(Town town)
    {
        for (int i = 0; i < _laneCells.Length; i++)
        {
            if (_laneCells[i].Length == 0 || _laneOpenAt[i] < 0) continue;
            float w = (float)Math.Clamp((town.Time - _laneOpenAt[i]) / LaneWearSeconds, 0, 1);
            float v = w >= 1f ? WornWear : TrailWear + (WornWear - TrailWear) * w;
            var l = _lanePlan!.Links[i];
            float front = _laneReveal[i] * (float)(l.Length + 3.0);
            var cells = _laneCells[i]; var dist = _laneDist[i];
            for (int k = 0; k < cells.Length; k++)
            {
                float a = Math.Clamp((front - dist[k]) / 3f, 0f, 1f);
                float t = v * a;
                if (t > _target[cells[k]]) _target[cells[k]] = t;
            }
        }
    }
}
