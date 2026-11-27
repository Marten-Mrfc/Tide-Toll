using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Speed carried over (round L3): every part of the menu's transitions remembers how fast it moved in the last frame,
/// and how fast that speed was changing (per second of the timeline). A transition that takes over another mid-way
/// (Esc while the book closes or opens, Back while the settings arrive, the question opened and closed quickly) adds
/// <see cref="BookMotion.Carry(float, float, float, float)"/> to each part's eased curve: the part leaves the takeover
/// at the speed and acceleration it had, and its own curve takes over by the end of its carry (at least
/// <see cref="CarryMin"/> seconds, at most the new transition's length), so it still ends where and when the new
/// transition says. From rest every speed is nothing and the curves are exactly the plain ones. Allocates nothing.
/// </summary>
public partial class PauseMenu
{
    /// <summary>The shortest a carried speed takes to give way, seconds (its acceleration stays gentle).</summary>
    public const float CarryMin = 0.35f;
    /// <summary>The shortest swing of a board turned round while it moves, seconds.</summary>
    public const float CarryBoard = 0.5f;

    // Parts: 0 interface, 1 lettering, 2 question, 3 settings spread, 4 board, 5 models, 6 camera log size.
    private const int PHud = 0, PLet = 1, PAsk = 2, PPag = 3, PCov = 4, PSink = 5, PSize = 6, Parts = 7;
    private readonly float[] _now = new float[Parts], _vel = new float[Parts], _acc = new float[Parts];
    private readonly float[] _cv = new float[Parts], _ca = new float[Parts], _cd = new float[Parts];
    private Vector3 _vPiv, _aPiv, _cvPiv, _caPiv;
    private float _covA, _sinkA, _camLS;
    private Vector3 _camP;
    private float _lastT;
    private int _measured;
    private bool _carry;

    /// <summary>The transition under way took over another and carries its parts' speeds (tests).</summary>
    public bool Carried => _carry;

    /// <summary>Called by Start once the parts' windows are set: keeps the speeds of a transition taken over, or none.</summary>
    private void PlanCarry(bool turning, float len)
    {
        if (!turning)
        {
            Array.Clear(_vel); Array.Clear(_acc);
            _vPiv = _aPiv = Vector3.Zero;
            _covA = _main.Cover.Amount; _sinkA = _main.Cover.Sink;
            _camP = _main.Rig.PivotLocal; _camLS = MathF.Log(Math.Max(1e-3f, _main.Rig.Camera.Size));
        }
        _carry = turning;
        for (int i = 0; i < Parts; i++) { _cv[i] = _vel[i]; _ca[i] = _measured >= 2 ? _acc[i] : 0f; }
        _cvPiv = _vPiv; _caPiv = _measured >= 2 ? _aPiv : Vector3.Zero;
        _cd[PHud] = CarryFor(_hud1, len); _cd[PLet] = CarryFor(_let1, len); _cd[PAsk] = CarryFor(_ask1, len); _cd[PPag] = CarryFor(_pag1, len);
        _cd[PCov] = CarryFor(_cov1, len); _cd[PSink] = CarryFor(_sink1, len); _cd[PSize] = CarryFor(_cam1, len);
        if (_cvPiv.LengthSquared() > 1e-8f || _caPiv.LengthSquared() > 1e-8f || _cv[PSize] != 0f || _ca[PSize] != 0f) _camMoves = true;
        _now[PHud] = _hudA; _now[PLet] = _letA; _now[PAsk] = _askA; _now[PPag] = _pagA;
        _now[PCov] = _covA; _now[PSink] = _sinkA; _now[PSize] = _camLS; _pivPrev = _camP;
        _lastT = 0f;
        // Taken over, the speeds go on from the old transition's, so the next change of speed is a true acceleration.
        _measured = turning ? 2 : 0;
    }

    private static float CarryFor(float end, float len) => Math.Min(len, Math.Max(CarryMin, end));

    private bool BoardMoves => _covTo != _covFrom || _cv[PCov] != 0f || _cv[PSink] != 0f || _ca[PCov] != 0f || _ca[PSink] != 0f;

    /// <summary>Part i's carried term at the timeline's time now.</summary>
    private float CarryOf(int i) => BookMotion.Carry(_cv[i], _ca[i], _cd[i], _motion.Time);

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>The speeds of this frame, from the change since the last frame of the timeline (end of Apply).</summary>
    private void Measure()
    {
        float dt = _motion.Time - _lastT;
        if (dt <= 1e-6f) return;
        float inv = 1f / dt;
        Track(PHud, _hudA, inv); Track(PLet, _letA, inv); Track(PAsk, _askA, inv); Track(PPag, _pagA, inv);
        Track(PCov, _covA, inv); Track(PSink, _sinkA, inv); Track(PSize, _camLS, inv);
        var v = (_camP - _pivPrev) * inv;
        _aPiv = (v - _vPiv) * inv; _vPiv = v; _pivPrev = _camP;
        _lastT = _motion.Time;
        _measured++;
    }

    private Vector3 _pivPrev;

    private void Track(int i, float x, float inv)
    {
        float v = (x - _now[i]) * inv;
        _acc[i] = (v - _vel[i]) * inv;
        _vel[i] = v;
        _now[i] = x;
    }
}
