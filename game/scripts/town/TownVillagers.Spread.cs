using System;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 (villagers as people, part 3): bodies, in the drawing only (the simulation is untouched). One pass a frame in place
/// of round 6's pairwise spread: walkers keep to the right of the way they go, wider on a paved road; a follower keeps a
/// figure's distance behind the one ahead on its road (bounded, eased); people standing about (at home, at the market,
/// at the landing, waiting at a crossing) take spots of their own around where the simulation puts them; and a bounded
/// push for what is left that never puts anyone in a building. A neighbour grid replaces comparing every pair. The
/// line of the first-age workplaces and the work spots (TownVillagers.Line.cs, WorkSites.cs) are kept as they are.
/// </summary>
public partial class TownVillagers
{
    /// <summary>Keep-right offsets from the road's middle by surface (track, gravel, cobbles), in cells.</summary>
    public static readonly float[] LaneCells = { 0.16f, 0.22f, 0.30f };
    /// <summary>A follower keeps this many metres (per unit of WalkerScale) behind the one ahead; lags at most FollowMaxLag of it.</summary>
    public const float FollowGapM = 1.1f, FollowMaxLag = 1.5f;
    private const int GridBits = 6, GridSize = 1 << GridBits, GridMask = GridSize - 1;
    /// <summary>n10: take the test-only gap measures (MinStandingGap, MinSpotGap, MinFollowGap) each frame; set by tests.</summary>
    public static bool MeasureGaps;
    private readonly int[] _gridHead = new int[GridSize * GridSize], _gridNext = new int[Capacity];
    private readonly bool[] _walkRoad = new bool[Capacity], _stood = new bool[Capacity], _standAbout = new bool[Capacity];
    private readonly int[] _road = new int[Capacity], _roadDir = new int[Capacity], _leader = new int[Capacity], _order = new int[Capacity];
    private readonly float[] _prog = new float[Capacity], _lag = new float[Capacity], _sx = new float[Capacity], _sy = new float[Capacity];
    private readonly float[] _side = new float[Capacity];
    private int[] _roadHead = Array.Empty<int>();
    private readonly int[] _roadNext = new int[Capacity];

    /// <summary>Tests: which side of its road a walker is drawn on (+1 right of the road's A to B direction, -1 left, 0 not a road walker).</summary>
    public int RoadSide(int i) => _walkRoad[i] ? (_side[i] * _roadDir[i] > 0 ? 1 : -1) : 0;
    public int RoadOf(int i) => _walkRoad[i] ? _road[i] : -1;
    /// <summary>Tests: the walker this one follows on its road and way (-1 none), and the smallest drawn gap between a follower and its leader this frame (metres).</summary>
    public int LeaderOf(int i) => _walkRoad[i] ? _leader[i] : -1;
    public float MinFollowGap { get; private set; } = float.MaxValue;
    /// <summary>Tests: the smallest drawn distance between two people standing about (not in a line, not at work) this frame (metres).</summary>
    public float MinSpotGap { get; private set; } = float.MaxValue;
    /// <summary>The drawn follow gap in metres now.</summary>
    public float FollowGapMetres => FollowGapM * WalkerScale;

    private static int GridKey(float x, float y, float g) => ((int)MathF.Floor(x / g) & GridMask) | (((int)MathF.Floor(y / g) & GridMask) << GridBits);

    private void Spread(Town t, int n, float dt)
    {
        float r = Personal * WalkerScale / Cell, gap = FollowGapM * WalkerScale / Cell;
        var plan = t.Plan;
        if (_roadHead.Length != plan.Roads.Length) _roadHead = new int[plan.Roads.Length]; // once per town
        Array.Fill(_roadHead, -1);
        // 1. Who walks which road which way, and where the simulation has them along it.
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            _walkRoad[i] = v.Phase == VillagerPhase.Walking && v.Road >= 0 && v.Road < plan.Roads.Length && !v.OnRiver && !_stood[i];
            _standAbout[i] = !_inLine[i] && !_atWork[i] && !_stood[i] && !_walkRoad[i] && v.Phase != VillagerPhase.Walking;
            _leader[i] = -1;
            if (!_walkRoad[i]) { _lag[i] = 0; continue; }
            var rd = plan.Roads[v.Road];
            _road[i] = v.Road; _roadDir[i] = v.Dir >= 0 ? 1 : -1;
            _prog[i] = (float)(v.Dir >= 0 ? v.Dist : rd.Length - v.Dist);
            _roadNext[i] = _roadHead[v.Road]; _roadHead[v.Road] = i;
        }
        // 2. Following distance: on each road and way, the one ahead first; each keeps `gap` behind the one ahead as
        //    drawn (bounded lag, eased below).
        float minFollow = float.MaxValue;
        for (int rr = 0; rr < _roadHead.Length; rr++)
        {
            if (_roadHead[rr] < 0) continue;
            for (int way = -1; way <= 1; way += 2)
            {
                int m = 0;
                for (int i = _roadHead[rr]; i >= 0; i = _roadNext[i])
                {
                    if (_roadDir[i] != way) continue;
                    // Insertion by progress, the one furthest along first (ties: the lower id leads).
                    int k = m++;
                    while (k > 0 && (_prog[_order[k - 1]] < _prog[i] || (_prog[_order[k - 1]] == _prog[i] && _order[k - 1] > i))) { _order[k] = _order[k - 1]; k--; }
                    _order[k] = i;
                }
                for (int k = 0; k < m; k++)
                {
                    int i = _order[k];
                    _lag[i] = 0;
                    if (k == 0) continue;
                    int l = _order[k - 1];
                    _leader[i] = l;
                    float ahead = (_prog[l] - _lag[l]) - _prog[i];
                    _lag[i] = Math.Clamp(gap - ahead, 0f, gap * FollowMaxLag);
                }
            }
        }
        // 3. The targets: keep right (by surface) and the follow lag along the curve, for walkers; a spot of their own for
        //    those standing about; then a bounded push from neighbours (grid), never into a building.
        float g = Math.Max(r, 0.5f);
        Array.Fill(_gridHead, -1);
        for (int i = 0; i < n; i++)
        {
            _sx[i] = _x[i]; _sy[i] = _y[i];
            int key = GridKey(_x[i], _y[i], g);
            _gridNext[i] = _gridHead[key]; _gridHead[key] = i;
        }
        float minGap = float.MaxValue, minSpot = float.MaxValue, k8 = Math.Min(1f, dt * 8f);
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            float wx = 0, wy = 0;
            if (_walkRoad[i])
            {
                var rd = plan.Roads[_road[i]];
                rd.At(v.Dist, out double x0, out double y0, out double h);
                double hw = _roadDir[i] > 0 ? h : h + Math.PI;
                float fx = (float)Math.Cos(hw), fy = (float)Math.Sin(hw);
                // Right of the way it goes (map y grows south): (-fy, fx).
                var surface = t.RoadInfo(_road[i]).Surface;
                float lane = LaneCells[Math.Clamp((int)surface, 0, LaneCells.Length - 1)] * Math.Clamp(WalkerScale / 2f, 0.8f, 1.25f);
                wx = -fy * lane; wy = fx * lane;
                // Which side of A to B this is: the right of A to B is (-sin h, cos h); the walker's offset dotted with it.
                _side[i] = wx * -(float)Math.Sin(h) + wy * (float)Math.Cos(h) >= 0 ? _roadDir[i] : -_roadDir[i];
                if (_lag[i] > 0)
                {
                    rd.At(v.Dist - _roadDir[i] * _lag[i], out double x1, out double y1, out _);
                    wx += (float)(x1 - x0); wy += (float)(y1 - y0);
                }
            }
            else if (_standAbout[i])
            {
                // A spot of one's own: those standing about at the same place spread round it on a sunflower spiral, by rank.
                int rank = 0;
                ForNeighbours(i, g, 0.35f, ref rank);
                if (rank > 0)
                {
                    float ang = rank * 2.39996f, rad = r * 1.15f * MathF.Sqrt(rank);
                    wx = MathF.Cos(ang) * rad; wy = MathF.Sin(ang) * rad;
                }
            }
            if (!_inLine[i] && !_atWork[i])
            {
                // What is left: a push from anyone drawn inside a body's width (bounded); walkers only across their road.
                float px = 0, py = 0;
                float cx = _x[i] + _ox[i], cy = _y[i] + _oy[i];
                int bx = (int)MathF.Floor(_x[i] / g), by = (int)MathF.Floor(_y[i] / g);
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        for (int j = _gridHead[((bx + ox) & GridMask) | (((by + oy) & GridMask) << GridBits)]; j >= 0; j = _gridNext[j])
                        {
                            if (j == i) continue;
                            float dx = cx - (_x[j] + _ox[j]), dy = cy - (_y[j] + _oy[j]), d2 = dx * dx + dy * dy;
                            if (d2 >= r * r) continue;
                            float d = MathF.Sqrt(d2);
                            if (d < 1e-3f) { float a2 = Math.Max(i, j) * 2.39996f * (i > j ? 1 : -1); px += MathF.Cos(a2) * r * 0.3f; py += MathF.Sin(a2) * r * 0.3f; }
                            else { px += dx / d * (r - d) * 0.35f; py += dy / d * (r - d) * 0.35f; }
                        }
                if (_walkRoad[i])
                {
                    var rd = plan.Roads[_road[i]];
                    rd.At(v.Dist, out _, out _, out double h);
                    float nx = -(float)Math.Sin(h), ny = (float)Math.Cos(h), along = px * nx + py * ny;
                    along = Math.Clamp(along, -0.08f, 0.08f);
                    px = nx * along; py = ny * along;
                }
                float pl = MathF.Sqrt(px * px + py * py);
                if (pl > 0.4f) { px *= 0.4f / pl; py *= 0.4f / pl; }
                wx += px; wy += py;
                if (!_walkRoad[i]) OutOfBuildings(t, _x[i] + wx, _y[i] + wy, ref wx, ref wy);
            }
            else { wx = 0; wy = 0; } // in a line or at work: the line's own spacing and the work spot
            float cap = _walkRoad[i] ? gap * FollowMaxLag + 0.5f : 1.6f;
            float wl = MathF.Sqrt(wx * wx + wy * wy);
            if (wl > cap) { wx *= cap / wl; wy *= cap / wl; }
            _ox[i] += (wx - _ox[i]) * k8;
            _oy[i] += (wy - _oy[i]) * k8;
        }
        // 4. Measures for the tests (neighbours only): standing gaps, spot gaps, follow gaps. n10 (research Q10): only
        // when a test asks (TownTestBase sets MeasureGaps); the game itself never reads them.
        for (int i = 0; MeasureGaps && i < n; i++)
        {
            var v = t.Villager(i);
            bool still = v.Phase is VillagerPhase.Queued or VillagerPhase.Working;
            float xi = _x[i] + _ox[i], yi = _y[i] + _oy[i];
            if (_walkRoad[i] && _leader[i] >= 0)
            {
                int l = _leader[i];
                float dx = xi - (_x[l] + _ox[l]), dy = yi - (_y[l] + _oy[l]);
                minFollow = Math.Min(minFollow, MathF.Sqrt(dx * dx + dy * dy) * Cell);
            }
            if (!still && !_standAbout[i]) continue;
            int bx = (int)MathF.Floor(_x[i] / g), by = (int)MathF.Floor(_y[i] / g);
            for (int oy = -2; oy <= 2; oy++)
                for (int ox = -2; ox <= 2; ox++)
                    for (int j = _gridHead[((bx + ox) & GridMask) | (((by + oy) & GridMask) << GridBits)]; j >= 0; j = _gridNext[j])
                    {
                        if (j >= i) continue;
                        float dx = xi - (_x[j] + _ox[j]), dy = yi - (_y[j] + _oy[j]), d = MathF.Sqrt(dx * dx + dy * dy) * Cell;
                        var u = t.Villager(j);
                        if (still && u.Phase is VillagerPhase.Queued or VillagerPhase.Working) minGap = Math.Min(minGap, d);
                        if (_standAbout[i] && _standAbout[j]) minSpot = Math.Min(minSpot, d);
                    }
        }
        MinStandingGap = minGap;
        MinSpotGap = minSpot;
        MinFollowGap = minFollow;
    }

    /// <summary>Counts those standing about, with a lower number, whose simulation spot is within <paramref name="near"/> cells of i's.</summary>
    private void ForNeighbours(int i, float g, float near, ref int rank)
    {
        int bx = (int)MathF.Floor(_sx[i] / g), by = (int)MathF.Floor(_sy[i] / g);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
                for (int j = _gridHead[((bx + ox) & GridMask) | (((by + oy) & GridMask) << GridBits)]; j >= 0; j = _gridNext[j])
                {
                    if (j >= i || !_standAbout[j]) continue;
                    float dx = _sx[i] - _sx[j], dy = _sy[i] - _sy[j];
                    if (dx * dx + dy * dy < near * near) rank++;
                }
    }

    /// <summary>
    /// Round 6 (W3): a villager whose drawn spot falls inside a built plot's footprint (the mill, a cottage, the
    /// landing's stall) is drawn just outside its nearest edge. Fields are walked in.
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
