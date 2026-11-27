using System;

namespace TideToll.Game;

/// <summary>
/// The question's hand-over turned round (last look, night 3): when the question is opened and closed again at once
/// (Esc or Keep this town while it comes in), the lettering is still fading out, a few hundredths of a second from its
/// end. Turning its speed round on the spot (the carry) made it jump and its speed jerk; instead it finishes its own
/// eased fade-out (it arrives at rest, nothing showing) and only then fades in on the new transition's curve. The same
/// for the question when the hand-over goes the other way. Only a part close to the end of its own, uncarried curve
/// is finished this way; everything else carries its speed as before. And Esc pressed on frames running: a
/// turn-round asked for in the first <see cref="TurnGap"/> of a transition is held until then (presses in between
/// count: an even number cancels out), so the parts have a real speed to carry and no move starts and turns round
/// within a frame. Allocates nothing.
/// </summary>
public partial class PauseMenu
{
    /// <summary>A part left closer than this to the end of its fade (seconds) finishes the fade before turning round.</summary>
    public const float TailMax = 0.12f;

    /// <summary>The rest of a fade taken over, on the new timeline.</summary>
    private struct Tail
    {
        public bool On;
        public float From, To, T0, T1;
        public readonly bool Covers(float t) => On && t < T1;
        public readonly float At(BookMotion m) => BookMotion.Mix(From, To, m.Ease(T0, T1));
    }

    /// <summary>The least time a transition runs before it may be turned round, seconds.</summary>
    public const float TurnGap = 0.1f;
    private bool _turnHeld;

    /// <summary>A turn-round asked for too soon: held (true) or not (false: act now).</summary>
    private bool HoldTurn()
    {
        if (!_motion.Running || _motion.Time >= TurnGap) return false;
        _turnHeld = !_turnHeld;
        return true;
    }

    /// <summary>Each frame, after the step: a held turn-round is made once the transition has run TurnGap.</summary>
    private void TurnHeld()
    {
        if (!_turnHeld) return;
        if (Now is not (State.Closing or State.Opening)) { _turnHeld = false; return; }
        if (_motion.Running && _motion.Time < TurnGap) return;
        _turnHeld = false;
        if (Now == State.Closing) Resume(); else Pause();
    }

    private Tail _letTail, _askTail;
    private float _oldT, _oLetFrom, _oLetTo, _oLet0, _oLet1, _oAskFrom, _oAskTo, _oAsk0, _oAsk1;
    private bool _oldTurning, _oldCarried;

    /// <summary>Start, first thing: the windows of the transition being taken over.</summary>
    private void KeepOld(bool turning)
    {
        _oldTurning = turning; _oldCarried = _carry || _letTail.On || _askTail.On; _oldT = _motion.Time;
        _oLetFrom = _letFrom; _oLetTo = _letTo; _oLet0 = _let0; _oLet1 = _let1;
        _oAskFrom = _askFrom; _oAskTo = _askTo; _oAsk0 = _ask0; _oAsk1 = _ask1;
        _letTail.On = false; _askTail.On = false;
    }

    /// <summary>Start, once the new windows are set: the lettering or the question finishes a fade it was about to end.</summary>
    private void PlanTails(Move move)
    {
        if (!_oldTurning || _oldCarried) return;
        if (TailOf(ref _letTail, _oLetFrom, _oLetTo, _oLet0, _oLet1, _letTo))
        {
            _letFrom = _letTail.To;
            // AskOut with nothing of the question shown or on its way: the lettering may come back as soon as its fade has ended.
            bool askAbsent = move == Move.AskOut && _askFrom <= 0f && _oldT <= _oAsk0;
            _let0 = askAbsent ? _letTail.T1 : Math.Max(_let0, _letTail.T1);
            _let1 = Math.Max(_let1, _let0 + 0.1f);
        }
        if (TailOf(ref _askTail, _oAskFrom, _oAskTo, _oAsk0, _oAsk1, _askTo))
        {
            _askFrom = _askTail.To;
            bool letAbsent = move == Move.AskIn && _letFrom <= 0f && _oldT <= _oLet0;
            _ask0 = letAbsent ? _askTail.T1 : Math.Max(_ask0, _askTail.T1);
            _ask1 = Math.Max(_ask1, _ask0 + 0.1f);
        }
    }

    private bool TailOf(ref Tail tail, float from, float to, float t0, float t1, float newTo)
    {
        float left = t1 - _oldT;
        if (to == from || newTo == to || _oldT <= t0 || left <= 0f || left > TailMax) return false;
        tail.On = true; tail.From = from; tail.To = to; tail.T0 = t0 - _oldT; tail.T1 = left;
        return true;
    }

    /// <summary>After PlanCarry: a part finishing its fade carries no speed of its own (its old curve is its speed).</summary>
    private void DropTailCarry()
    {
        if (_letTail.On) { _cv[PLet] = 0f; _ca[PLet] = 0f; }
        if (_askTail.On) { _cv[PAsk] = 0f; _ca[PAsk] = 0f; }
    }
}
