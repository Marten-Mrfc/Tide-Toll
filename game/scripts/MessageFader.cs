using System;

namespace TideToll.Game;

/// <summary>
/// Night 3 (message-system.md section 3): a control that comes and goes with the town's state (the goal's content, the
/// strip, the line, a tag) fades by a <see cref="BookMotion"/> timeline of fixed length instead of flipping
/// <c>Visible</c>. <see cref="Want"/> sets where it should be; a change mid-way starts from where it is (the same
/// eased curve, scaled to the distance left), so the value is continuous. Allocates nothing.
/// </summary>
public sealed class MessageFader
{
    private readonly BookMotion _m = new();
    private float _from, _to, _v;
    public float Seconds;

    public MessageFader(float seconds, bool on = false) { Seconds = seconds; _v = _from = _to = on ? 1f : 0f; }

    /// <summary>0..1, eased.</summary>
    public float Value => _v;
    public bool On => _to > 0.5f;
    public bool Moving => _m.Running;
    public BookMotion Motion => _m;

    public void Want(bool on)
    {
        float to = on ? 1f : 0f;
        if (to == _to) return;
        _from = _v; _to = to;
        _m.Start(Seconds * Math.Max(0.25f, MathF.Abs(_to - _from)));
    }

    /// <summary>Jumps to the end (a rebuild, a new town).</summary>
    public void Set(bool on) { _m.Stop(); _v = _from = _to = on ? 1f : 0f; }

    public void Step(float dt)
    {
        if (!_m.Running) { _v = _to; return; }
        _m.Advance(dt);
        _v = BookMotion.Mix(_from, _to, _m.Ease(0f, _m.Length));
        if (!_m.Running) _v = _to;
    }
}
