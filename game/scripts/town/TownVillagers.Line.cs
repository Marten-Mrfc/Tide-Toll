using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Work you can watch (n5), A: the line. Those who wait at the well, a field or the mill stand in a line along the lane
/// the first of them came by (<see cref="WorkSites.Lane"/>), <see cref="WorkSites.LineStep"/> apart (more with a
/// handcart), each facing the workplace, the first nearest to it; a line longer than its lane bends on along the next
/// road (<see cref="WorkSites.LinePoint"/>), a third of a cell aside on the lane's right-hand edge (<see cref="WorkSites.LineStand"/>). When a place comes free the line moves up one step, front first: each
/// place starts a little after the one before it (<see cref="RippleSeconds"/>) and eases along the lane. Drawing only:
/// the queue's order and length are the simulation's (<see cref="VillagerView.QueuePosition"/>).
/// </summary>
public partial class TownVillagers
{
    /// <summary>Delay between one place in the line starting to move up and the next behind it, real seconds at 1x.</summary>
    public const float RippleSeconds = 0.14f;
    private const int MaxWorkplaces = 48;
    private readonly int[] _lane = new int[MaxWorkplaces], _holders = new int[MaxWorkplaces];
    private readonly bool[] _inLine = new bool[Capacity], _atWork = new bool[Capacity];
    private readonly int[] _lineWp = new int[Capacity], _qt = new int[Capacity];
    private readonly float[] _qd = new float[Capacity], _qdelay = new float[Capacity];
    private readonly float[] _lineX = new float[Capacity], _lineY = new float[Capacity], _lineFace = new float[Capacity];

    /// <summary>Tests: whether villager i is drawn in a workplace's line, has reached its place in it, its place (drawn).</summary>
    public bool InLine(int i) => _inLine[i];
    public bool LineSettled(int i) => _inLine[i] && MathF.Abs(_qd[i] - _qt[i]) < 0.01f && MathF.Abs(_x[i] - _lineX[i]) + MathF.Abs(_y[i] - _lineY[i]) < 0.03f
                                      && MathF.Abs(_ox[i]) + MathF.Abs(_oy[i]) < 0.03f && MathF.Abs(Wrap(_yaw[i] - _lineFace[i])) < 0.05f;
    public float LinePlace(int i) => _qd[i];
    public int LaneOf(int workplace) => workplace >= 0 && workplace < MaxWorkplaces ? _lane[workplace] : -1;
    /// <summary>Where villager i is drawn this frame (map cells, with the drawn-only spacing), and the yaw it faces (model +Z).</summary>
    public Vector2 DrawnAt(int i) => new(_x[i] + _ox[i], _y[i] + _oy[i]);
    public float DrawnYaw(int i) => _yaw[i];
    /// <summary>Gap between two in a line at the town's Load level, in cells.</summary>
    public static float LineStepCells(int loadLevel) => (CartAt(loadLevel) ? WorkSites.LineStepCart : WorkSites.LineStep) / Cell;

    private static bool IsWorkplaceKind(PlotKind k) => k is PlotKind.Well or PlotKind.Field or PlotKind.Mill;

    /// <summary>Once a frame: the lane of each workplace's line, kept while its line stands (so the line never jumps lanes).</summary>
    private void Lanes(Town t)
    {
        int nw = Math.Min(t.WorkplaceCount, MaxWorkplaces);
        for (int w = 0; w < nw; w++)
        {
            var wp = t.Workplace(w);
            _holders[w] = 0;
            if (wp.QueueLength == 0 || !IsWorkplaceKind(wp.Kind)) _lane[w] = -1;
            else if (_lane[w] < 0) _lane[w] = WorkSites.Lane(t, w);
        }
    }

    /// <summary>
    /// The drawn target of villager i, standing in a line or at work (map cells), and the yaw it faces there. Returns
    /// false when the simulation's own position stands (walking, idle, the river's waits).
    /// </summary>
    private bool StandTarget(Town t, int i, in VillagerView v, float dt, ref float tx, ref float ty, ref float face)
    {
        _atWork[i] = false;
        bool queued = v.Phase == VillagerPhase.Queued && v.Workplace >= 0 && v.Workplace < MaxWorkplaces && _lane[v.Workplace] >= 0;
        if (!queued) _inLine[i] = false;
        if (queued)
        {
            int w = v.Workplace, pos = Math.Max(0, v.QueuePosition);
            float speed = (float)Math.Max(1, _drv.Speed);
            if (!_inLine[i] || _lineWp[i] != w) { _inLine[i] = true; _lineWp[i] = w; _qd[i] = pos; _qt[i] = pos; _qdelay[i] = 0; }
            else if (pos < _qt[i] - 2)
            {
                // More than two places at once: the town ran ahead without frames (a fast-forward). The place is taken
                // at once and the drawn villager follows it like any jump (the chase, or a snap beyond SnapCells).
                _qd[i] = pos; _qt[i] = pos; _qdelay[i] = 0;
            }
            else if (pos != _qt[i])
            {
                // Moving up: front first, each place a little after the one ahead of it.
                _qdelay[i] = pos < _qt[i] ? RippleSeconds * pos / Math.Max(1f, speed * 0.5f) : 0f;
                _qt[i] = pos;
            }
            if (_qdelay[i] > 0) _qdelay[i] -= dt;
            else if (_qd[i] != _qt[i])
            {
                float step = LineStepCells(t.LoadLevel);
                // At walking pace along the lane, slowing over the last third of a step (eased in and out).
                float rate = (float)TownRules.BaseSpeed / step * speed * dt;
                float left = _qt[i] - _qd[i];
                float k = rate * Math.Clamp(MathF.Abs(left) * 3f + 0.2f, 0.2f, 1f);
                _qd[i] = MathF.Abs(left) <= k ? _qt[i] : _qd[i] + MathF.Sign(left) * k;
            }
            float gap = LineStepCells(t.LoadLevel);
            WorkSites.LineStand(t, w, _lane[w], gap * (1f + _qd[i]), gap, out float lx, out float ly, out float lh);
            _lineX[i] = lx; _lineY[i] = ly;
            tx = lx; ty = ly;
            if (_qd[i] < 0.5f)
            {
                WorkSites.LineHead(t, t.Workplace(w).Plot, out float hx, out float hy);
                face = HeadingYaw(Math.Atan2(hy - ly, hx - lx));
            }
            else face = HeadingYaw(lh);
            _lineFace[i] = face;
            return true;
        }
        // Coming along the lane to a line: stop at its tail (do not walk past the line to the door and back).
        if (v.Phase == VillagerPhase.Walking && v.Workplace >= 0 && v.Workplace < MaxWorkplaces && _lane[v.Workplace] >= 0 && v.Road == _lane[v.Workplace])
        {
            int w = v.Workplace;
            var plan = t.Plan;
            var rd = plan.Roads[v.Road];
            int node = plan.Plots[t.Workplace(w).Plot].Node;
            if (rd.A == node || rd.B == node)
            {
                float fromNode = (float)(rd.A == node ? v.Dist : rd.Length - v.Dist);
                float gap = LineStepCells(t.LoadLevel);
                float tail = gap * (1f + t.Workplace(w).QueueLength + _holders[w]);
                if (fromNode < tail + 0.05f)
                {
                    _holders[w]++;
                    WorkSites.LineStand(t, w, _lane[w], tail, gap, out float lx, out float ly, out float lh);
                    tx = lx; ty = ly; face = HeadingYaw(lh);
                    return true;
                }
            }
        }
        if (v.Phase == VillagerPhase.Working && v.Workplace >= 0)
        {
            var wp = t.Workplace(v.Workplace);
            if (!IsWorkplaceKind(wp.Kind)) return false;
            WorkSites.WorkSpot(t, wp.Plot, _slot[i], out float wx, out float wy, out float wh);
            tx = wx; ty = wy; face = HeadingYaw(wh);
            _atWork[i] = true;
            return true;
        }
        // Taking up the goods: stay at the spot for the first part of the lift (not when the town runs fast).
        if (_take[i] > WorkSites.TakeSeconds * 0.4f && _drv.Speed <= 4)
        {
            float dx = tx - _holdX[i], dy = ty - _holdY[i];
            if (dx * dx + dy * dy < 1.5f * 1.5f) { tx = _holdX[i]; ty = _holdY[i]; face = _yaw[i]; return true; }
        }
        return false;
    }
}
