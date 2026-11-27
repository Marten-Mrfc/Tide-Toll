using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Work you can watch (n5), B: the villager at work. A pose and a rhythm per job, never an act that needs seconds (work
/// is short and gets shorter): at the well a rock forward and back with the crank; in a field bent about 30 degrees,
/// swinging from side to side and stepping slowly along the row with a sickle (a scythe, with a wider swing, from the
/// first Tools milestone); at the mill the grain sack tipped in at the door, then a slow sway on the step. As work ends
/// (estimated from the work time the town would give, corrected by what was seen) the worker stoops; the goods come up
/// into the arms only once the simulation has handed them over (<see cref="VillagerView.Carry"/>), rising from where
/// they stood (<see cref="WorkSites.Goods"/>). When the bell rings workers and those in line lift their heads for a
/// moment and the rhythm quickens while it rings. Every weight is eased (0.15 s or longer, at any frame rate).
/// </summary>
public partial class TownVillagers
{
    public const float TipSeconds = 0.45f, BellLiftSeconds = 1.1f;
    private readonly VillagerPhase[] _pPhase = new VillagerPhase[Capacity];
    private readonly int[] _pWp = new int[Capacity], _slot = new int[Capacity];
    private readonly Good[] _pCarry = new Good[Capacity];
    private readonly byte[] _kind = new byte[Capacity];          // pose kind: 0 none, 1 well, 2 field, 3 mill
    private readonly float[] _poseW = new float[Capacity], _rh = new float[Capacity], _stoop = new float[Capacity], _fwd = new float[Capacity];
    private readonly double[] _workStart = new double[Capacity];
    private readonly float[] _take = new float[Capacity], _tip = new float[Capacity], _holdX = new float[Capacity], _holdY = new float[Capacity];
    private readonly Vector3[] _takeFrom = new Vector3[Capacity], _tipTo = new Vector3[Capacity];
    private readonly float[] _pitch = new float[Capacity], _swing = new float[Capacity], _crouch = new float[Capacity];
    private readonly float[] _ratio = { 1f, 1f, 1f, 1f };       // seen / expected work time, per pose kind
    private float _bellLift;

    /// <summary>Tests: the largest rate of change of any villager's pose this frame (radians per second of frame time).</summary>
    public float MaxPoseRate { get; private set; }
    /// <summary>Tests: the pose weight (0 standing .. 1 in the job's pose), the stoop, the body's lean (radians) of villager i.</summary>
    public float PoseWeight(int i) => _poseW[i];
    public float Stoop(int i) => _stoop[i];
    public float Pitch(int i) => _pitch[i];
    /// <summary>Tests: how far the heads are lifted for the bell (0..1), and the rhythm's tempo factor now.</summary>
    public float HeadsUp { get; private set; }
    public float Tempo { get; private set; } = 1f;

    private static byte KindOf(PlotKind k) => k switch { PlotKind.Well => 1, PlotKind.Field => 2, PlotKind.Mill => 3, _ => 0 };

    /// <summary>Seconds of work the town gives a job of pose kind k now (before the bell), corrected by what was seen.</summary>
    private float ExpectedWork(Town t, int k)
    {
        double w = k == 1 ? TownRules.WellWork : k == 2 ? TownRules.FieldWork : TownRules.MillWork;
        return (float)(w / Town.ToolsFactorAt(t.ToolsLevel)) * _ratio[k];
    }

    /// <summary>Once a frame: the bell's moment of heads up, and the tempo.</summary>
    private void WorkFrame(Town t, float dt)
    {
        var ev = _drv.Events;
        for (int e = 0; e < ev.Count; e++) if (ev[e].Kind == TownEventKind.BellRung) _bellLift = BellLiftSeconds;
        _bellLift = Math.Max(0f, _bellLift - dt);
        HeadsUp = _bellLift > 0 ? MathF.Sin(MathF.PI * (1f - _bellLift / BellLiftSeconds)) : 0f;
        Tempo = (t.BellRinging ? (float)TownRules.BellHurry : 1f) * (float)Math.Min(Math.Max(1, _drv.Speed), 2);
    }

    /// <summary>A villager's changes since the last frame: work begun, work finished (goods taken up), a load delivered.</summary>
    private void Transitions(Town t, int i, in VillagerView v, bool fresh)
    {
        if (fresh)
        {
            _pPhase[i] = v.Phase; _pWp[i] = v.Workplace; _pCarry[i] = v.Carry;
            if (_fly[i] > 0 && _flyPlot[i] >= 0) WorkSites.Delivered(_flyPlot[i], i, _flyGood[i], _flyAmount[i]);
            _take[i] = _tip[i] = _fly[i] = 0; _poseW[i] = 0; _stoop[i] = 0; _inLine[i] = false; _handOff[i] = true; _seenPose[i] = false; _park[i] = 0;
            if (v.Phase == VillagerPhase.Working && v.Workplace >= 0)
            {
                var wp = t.Workplace(v.Workplace);
                _slot[i] = WorkSites.SlotOf(t, wp.Plot, v.X, v.Y); _kind[i] = KindOf(wp.Kind); _workStart[i] = t.Time; _poseW[i] = 1; _park[i] = 1;
            }
            return;
        }
        var pp = _pPhase[i];
        if (v.Phase == VillagerPhase.Working && pp != VillagerPhase.Working && v.Workplace >= 0)
        {
            var wp = t.Workplace(v.Workplace);
            _slot[i] = WorkSites.SlotOf(t, wp.Plot, v.X, v.Y);
            _kind[i] = KindOf(wp.Kind);
            _workStart[i] = t.Time;
            WorkSites.Started(wp.Plot, i, _slot[i], v.Carry);
            // The mill: the sack carried in goes in at the door.
            if (v.Carry != Good.None) { _tip[i] = TipSeconds; _tipTo[i] = WorkSites.Doorway(t, wp.Plot); }
        }
        if (pp == VillagerPhase.Working && v.Phase != VillagerPhase.Working && _pWp[i] >= 0 && _pWp[i] < t.WorkplaceCount)
        {
            var wp = t.Workplace(_pWp[i]);
            int k = _kind[i];
            if (k > 0 && !t.BellRinging)
            {
                double seen = t.Time - _workStart[i], want = ExpectedWork(t, k) / _ratio[k];
                if (seen > 0.05 && want > 0.05) _ratio[k] = Math.Clamp((float)(seen / want), 0.3f, 3f);
            }
            WorkSites.Finished(wp.Plot, i, _slot[i], v.Carry);
            _tip[i] = 0;
            if (v.Carry != Good.None)
            {
                _take[i] = WorkSites.TakeSeconds;
                _takeFrom[i] = WorkSites.Goods(t, wp.Plot, _slot[i]);
                _holdX[i] = _x[i]; _holdY[i] = _y[i];
                _handOff[i] = true;
            }
        }
        if (_pCarry[i] != Good.None && v.Carry == Good.None) StartDrop(t, i, _pCarry[i]);
        _pPhase[i] = v.Phase; _pWp[i] = v.Workplace; _pCarry[i] = v.Carry;
    }

    /// <summary>
    /// The pose of villager i this frame: the lean (radians, forward positive, about the feet), the crouch (height
    /// factor), a yaw swing, and a small step forward (metres).
    /// </summary>
    private void Pose(Town t, int i, in VillagerView v, float dt, out float pitch, out float crouch, out float swing, out float fwd)
    {
        bool working = _atWork[i] && _kind[i] > 0;
        _poseW[i] = Mathf.MoveToward(_poseW[i], working ? 1f : 0f, dt * 4f);
        _take[i] = Math.Max(0f, _take[i] - dt);
        if (_tip[i] > 0) { _tip[i] = Math.Max(0f, _tip[i] - dt); if (_tip[i] == 0) _handOff[i] = true; }
        float el = (float)(t.Time - _workStart[i]), dur = working ? ExpectedWork(t, _kind[i]) : 1f;
        float left = (dur - el) / (t.BellRinging ? (float)TownRules.BellHurry : 1f);
        float window = 0.25f * (float)Math.Max(1, _drv.Speed);
        bool stoop = (working && left < window) || _take[i] > 0;
        _stoop[i] = Mathf.MoveToward(_stoop[i], stoop ? 1f : 0f, dt * 5.5f);
        int k = _kind[i];
        float tempo = k == 1 ? 0.9f : k == 2 ? 0.75f : 0.55f;
        if (_poseW[i] > 0) _rh[i] += dt * MathF.Tau * tempo * Tempo;
        float s = MathF.Sin(_rh[i]), w = _poseW[i];
        float p = 0, c = 1, sw = 0, f = 0;
        if (k == 1) { p = 0.30f + 0.14f * s; f = 0.12f * s; }
        else if (k == 2)
        {
            bool scythe = ScytheAt(t.ToolsLevel);
            p = 0.52f + 0.04f * MathF.Sin(_rh[i] * 2f); c = 0.92f; sw = (scythe ? 0.55f : 0.38f) * s;
            // Stepping slowly along the row with the cut.
            if (working) _fwd[i] = Math.Clamp(el / Math.Max(0.1f, dur), 0f, 1f) * 0.8f;
        }
        else if (k == 3)
        {
            float tip = _tip[i] > 0 ? MathF.Sin(MathF.PI * (1f - _tip[i] / TipSeconds)) : 0f;
            p = 0.16f + 0.06f * s + 0.3f * tip;
        }
        if (!working || k != 2) _fwd[i] = Mathf.MoveToward(_fwd[i], 0f, dt * 1.2f);
        pitch = p * w; crouch = 1f + (c - 1f) * w; swing = sw * w; fwd = f * w + _fwd[i];
        float st = _stoop[i] * _stoop[i] * (3f - 2f * _stoop[i]);
        pitch += (0.56f - pitch) * st;
        crouch += (0.89f - crouch) * st;
        // The market: a dip of the body as the load leaves the arms.
        if (_fly[i] > 0 && !_flyHome[i]) pitch += 0.3f * MathF.Sin(MathF.PI * (1f - _fly[i] / WorkSites.DropSeconds));
        // The bell: heads up for a moment (workers and those in line).
        _headW[i] = Mathf.MoveToward(_headW[i], working || _inLine[i] ? 1f : 0f, dt * 5f);
        pitch -= (pitch + 0.1f) * HeadsUp * 0.8f * _headW[i];
        pitch = Math.Clamp(pitch, -0.12f, 0.6f);
        float rate = (MathF.Abs(pitch - _pitch[i]) + MathF.Abs(swing - _swing[i]) + MathF.Abs(crouch - _crouch[i])) / Math.Max(dt, 1e-4f);
        if (_seenPose[i]) MaxPoseRate = Math.Max(MaxPoseRate, rate);
        _seenPose[i] = true;
        _pitch[i] = pitch; _swing[i] = swing; _crouch[i] = crouch;
    }

    private readonly bool[] _seenPose = new bool[Capacity];
    private readonly float[] _headW = new float[Capacity];

    /// <summary>D: the scythe replaces the sickle from the first Tools milestone (TownRules.MilestoneEvery, step 5).</summary>
    public static bool ScytheAt(int toolsLevel) => toolsLevel >= TownRules.MilestoneEvery;
}
