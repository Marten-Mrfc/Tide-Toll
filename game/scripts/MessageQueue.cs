using System;
using System.Collections.Generic;

namespace TideToll.Game;

/// <summary>
/// Night 3 (message-system.md section 1): the queue of one message zone. Messages wait their turn, highest priority
/// first (first come first within a priority); one shows at a time. Each arrives and leaves by a <see cref="BookMotion"/>
/// timeline of fixed length (eased, real time, never more than <see cref="BookMotion.MaxStep"/> a frame), stays its
/// length at full, and leaves a gap before the next. With <see cref="CrowdAt"/> or more waiting each stays
/// <see cref="CrowdStay"/>; beyond <see cref="Cap"/> waiting the lowest is dropped (handed to <see cref="Overflow"/>).
/// While the zone may not show (<c>canShow</c> false) the one showing leaves and comes back first, and the waiting
/// ones age: one that has waited <see cref="WaitLimit"/> is handed to <see cref="Overflow"/>. Allocates nothing per
/// frame (only when a message is pushed).
/// </summary>
public sealed class MessageQueue
{
    public struct Item
    {
        public string Head, Text, Sound;
        public int Priority, Kind;
        /// <summary>Seconds at full; 0 or less: until clicked away.</summary>
        public float Stay;
        public bool Strong;
        /// <summary>Shown before and taken back (its sound does not play again).</summary>
        public bool Again;
        public float Waited;
        public int Seq;
        /// <summary>What the message is about (a level's number), for <see cref="Stale"/>.</summary>
        public int About;
        public bool UntilClicked => Stay <= 0f;
    }

    public enum Phase { Idle, In, Hold, Out, Gap }

    /// <summary>
    /// The longest step one frame adds to a message's stay, its gap and its wait, seconds. Stays are counted in real time
    /// (L2 review: counted in <see cref="BookMotion.MaxStep"/> steps, a 3.2 s banner stayed a minute at 1 frame a second);
    /// only a stall longer than this is not counted whole, so a message is never eaten by one long frame.
    /// </summary>
    public const float ClockCap = 0.5f;
    public static float Clock(float dt) => Math.Clamp(dt, 0f, ClockCap);
    /// <summary>A message about to show that is no longer true (a level the town has left); it is dropped unseen.</summary>
    public Func<Item, bool>? Stale;

    public float InSeconds = 0.2f, OutSeconds = 0.2f, GapSeconds = 0.15f, CrowdStay = 2f, WaitLimit = -1f;
    public int CrowdAt = 4, Cap = 6;
    /// <summary>A message that could not wait (too long blocked, or dropped by the cap); null drops it.</summary>
    public Action<Item>? Overflow;
    /// <summary>Called on the frame a message begins to arrive (its sound plays then).</summary>
    public Action<Item>? Shown;

    private readonly List<Item> _wait = new(16);
    private readonly BookMotion _m = new();
    private float _held, _gap, _alpha, _lastStay;
    private int _seq;

    public Phase Now { get; private set; }
    public Item Current { get; private set; }
    /// <summary>Opacity of the message showing, eased 0..1 (also its place: 0 is off by the zone's offset).</summary>
    public float Alpha => _alpha;
    public bool Showing => Now is Phase.In or Phase.Hold or Phase.Out;
    public int Waiting => _wait.Count;
    /// <summary>Messages shown so far (each counted once, when it starts to arrive).</summary>
    public int ShownCount { get; private set; }
    /// <summary>The stay the message showing now has (seconds; 0: until clicked).</summary>
    public float StayNow => _lastStay;
    /// <summary>Seconds the message showing has been at full.</summary>
    public float HeldFor => _held;
    public BookMotion Motion => _m;

    public Item Waiter(int i) => _wait[i];

    public void Push(string head, string text, int priority, float stay, string sound = "", bool strong = false, int kind = 0, int about = 0)
    {
        var it = new Item { Head = head, Text = text, Priority = priority, Stay = stay, Sound = sound, Strong = strong, Kind = kind, About = about, Seq = ++_seq };
        // The same message showing gets its time again; one already waiting is not queued twice.
        if (Showing && Current.Text == it.Text && Current.Head == it.Head) { if (Now == Phase.Hold) _held = 0f; return; }
        for (int i = 0; i < _wait.Count; i++) if (_wait[i].Text == it.Text && _wait[i].Head == it.Head) return;
        Insert(it);
        while (Cap > 0 && _wait.Count > Cap)
        {
            var low = _wait[^1];
            _wait.RemoveAt(_wait.Count - 1);
            Overflow?.Invoke(low);
        }
    }

    private void Insert(in Item it)
    {
        int at = _wait.Count;
        for (int i = 0; i < _wait.Count; i++) if (_wait[i].Priority < it.Priority) { at = i; break; }
        _wait.Insert(at, it);
    }

    /// <summary>Drops the waiting messages of this kind (a newer one says the same thing better); returns how many.</summary>
    public int DropWaiting(int kind)
    {
        int n = 0;
        for (int i = _wait.Count - 1; i >= 0; i--) if (_wait[i].Kind == kind) { _wait.RemoveAt(i); n++; }
        return n;
    }

    /// <summary>The player clicked the message away: it leaves now and the next follows.</summary>
    public bool Dismiss()
    {
        if (Now is not (Phase.In or Phase.Hold)) return false;
        Leave();
        return true;
    }

    /// <summary>Empties the zone at once (a new town): nothing shows and nothing waits.</summary>
    public void Clear()
    {
        _wait.Clear();
        _m.Stop();
        Now = Phase.Idle;
        _alpha = 0f;
    }

    private void Leave()
    {
        // From wherever the arrival has got to: the leave takes the same share of its length (continuous).
        float from = _alpha;
        _m.Start(OutSeconds);
        if (from < 1f) { for (int i = 0; i < 64 && 1f - BookMotion.Smoother(_m.Progress) > from; i++) _m.Advance(OutSeconds / 64f); }
        Now = Phase.Out;
    }

    /// <summary>One frame. <paramref name="canShow"/>: the zone may show a message now.</summary>
    public void Step(float dt, bool canShow)
    {
        float step = Clock(dt);
        if (!canShow && WaitLimit > 0f)
        {
            for (int i = _wait.Count - 1; i >= 0; i--)
            {
                var w = _wait[i];
                w.Waited += step;
                if (w.Waited >= WaitLimit) { _wait.RemoveAt(i); Overflow?.Invoke(w); }
                else _wait[i] = w;
            }
        }
        switch (Now)
        {
            case Phase.Idle:
                if (canShow && _wait.Count > 0) Begin();
                break;
            case Phase.In:
                if (!canShow) { Back(); break; }
                _m.Advance(dt);
                _alpha = _m.Ease(0f, _m.Length);
                if (!_m.Running) { Now = Phase.Hold; _held = 0f; _alpha = 1f; }
                break;
            case Phase.Hold:
                if (!canShow) { Back(); break; }
                _held += step;
                if (Stale != null && !Current.UntilClicked && Stale(Current)) { Leave(); break; }
                _lastStay = Current.UntilClicked ? 0f : _wait.Count >= CrowdAt ? Math.Min(Current.Stay, CrowdStay) : Current.Stay;
                if (!Current.UntilClicked && _held >= _lastStay) Leave();
                break;
            case Phase.Out:
                _m.Advance(dt);
                _alpha = 1f - _m.Ease(0f, _m.Length);
                if (!_m.Running) { _alpha = 0f; Now = Phase.Gap; _gap = 0f; }
                break;
            case Phase.Gap:
                _gap += step;
                if (_gap >= GapSeconds) { Now = Phase.Idle; if (canShow && _wait.Count > 0) Begin(); }
                break;
        }
    }

    /// <summary>The zone was taken (a choice, a page, the zoom): the message showing leaves and waits again, first.</summary>
    private void Back()
    {
        var c = Current;
        if (Now == Phase.Hold && !c.UntilClicked && _held >= c.Stay * 0.75f) { Leave(); return; } // seen enough
        c.Waited = 0f;
        c.Again = true;
        _wait.Insert(0, c);
        Leave();
    }

    private void Begin()
    {
        // Checked when it is about to show, not when it was queued: the town may have moved on while it waited.
        while (Stale != null && _wait.Count > 0 && Stale(_wait[0])) _wait.RemoveAt(0);
        if (_wait.Count == 0) { Now = Phase.Idle; return; }
        Current = _wait[0];
        _wait.RemoveAt(0);
        _m.Start(InSeconds);
        _alpha = 0f;
        _held = 0f;
        _lastStay = Current.Stay;
        Now = Phase.In;
        if (!Current.Again) ShownCount++;
        if (!Current.Again) Shown?.Invoke(Current);
    }
}
