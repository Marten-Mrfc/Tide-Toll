using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The menu's transitions, each one BookMotion timeline that every part reads its place from: the interface's opacity,
/// the camera (an eased path of fixed length, its size eased in log space, the tilt following the size), the board
/// (BookCover.Set; it lands with BookMotion.Land: no speed as it touches, a hair of settle), the town's models (sinking
/// with the board), the cover's lettering, the question and the settings spread (opacity). Each part has a window of the
/// timeline; neighbouring windows overlap so the whole reads as one motion. A transition starts from wherever every part
/// is now, so one asked for in the middle of another (Esc while the book closes, Back while the spread arrives) turns
/// round smoothly; its parts then start at once instead of waiting their turn. Nothing on screen is switched on or off:
/// a part is made visible at the start of its fade in (at no opacity) and hidden at the end of its fade out.
/// The save of a pause is written once a transition has ended (the frame after its last), never in one of its frames.
/// </summary>
public partial class PauseMenu
{
    public enum Move { None, Close, Open, ToSettings, FromSettings, AskIn, AskOut }

    private readonly BookMotion _motion = new();
    /// <summary>The transition under way (tests and frame strips can hold its clock).</summary>
    public BookMotion Motion => _motion;
    public Move Moving => _motion.Running ? _move : Move.None;
    /// <summary>The kind of the last transition started (tests).</summary>
    public Move LastMove => _move;
    /// <summary>Saves written by the menu (tests: the frame it happened in).</summary>
    public int MenuSaves { get; private set; }

    private Move _move;
    // Where each part starts and ends, and its window on the timeline (seconds).
    private float _hudFrom, _hudTo, _hud0, _hud1;
    private float _letFrom, _letTo, _let0, _let1;
    private float _askFrom, _askTo, _ask0, _ask1;
    private float _pagFrom, _pagTo, _pag0, _pag1;
    private float _covFrom, _covTo, _cov0, _cov1, _touch;
    private float _sinkFrom, _sinkTo, _sink0, _sink1;
    private bool _covLand, _camMoves;
    private Vector3 _camFromPivot, _camToPivot;
    private float _camFromSize, _camToSize, _cam0, _cam1;
    private float _hudA = 1f, _letA, _askA, _pagA;
    private float _askSlide = 1f;
    private bool _savePending;
    private Button? _focusOnLetters;

    /// <summary>Opacity of each part now (tests and frame strips).</summary>
    public float HudAlpha => _hudA;
    public float LetteringAlpha => _letA;
    public float QuestionAlpha => _askA;
    public float PagesAlpha => _pagA;
    /// <summary>The settings leaf's window on the timeline now (seconds): frame strips hold the clock at its ends.</summary>
    public float LeafStart => _pag0; public float LeafEnd => _pag1;

    // Lengths (seconds) of each transition when it starts from rest.
    public const float CloseSeconds = 1.25f, OpenSeconds = 0.95f, ToSettingsSeconds = 0.9f, FromSettingsSeconds = 1.05f,
        AskSeconds = 0.3f;
    /// <summary>The leaf bringing the settings pages in, and turning back to the town's pages, seconds.</summary>
    /// <summary>The question and the lettering hand over: the one leaving is out by AskOutEnd, the one coming starts at AskInStart (never both above 15 %).</summary>
    public const float AskOutEnd = 0.16f, AskInStart = 0.13f;
    public const float LeafSeconds = 0.3f, LeafBackSeconds = 0.27f;
    /// <summary>The board's swing down (to its touch and settle) and up, seconds, for a full swing.</summary>
    private const float SwingDown = 0.7f, SwingUp = 0.55f, Touch = 0.8f;

    // ------------------------------------------------------------------ the flow

    public void Pause()
    {
        if (Now == State.Playing)
        {
            if (_main.Hud.PaveMode) _main.Hud.SetPaveMode(false);
            if (_main.Hud.ExtendMode) _main.Hud.SetExtendMode(false);
            var rig = _main.Rig;
            _keptPivot = rig.PivotLocal; _keptSize = rig.Camera.Size; _keptMoved = rig.UserMoved;
            _followAfter = false;
            _main.Driver.SetPaused(true);
            rig.InputLocked = true;
            rig.UserMoved = true;
            string name = TownName.Of(_main.Driver.Seed);
            if (_name.Text != name) _name.Text = name;
            _main.Audio.SetPauseMusic(true);
            // Round 4: the save takes the view kept from before the pause (Kept), not the closed-book zoom; round 7: it is
            // written once the book is still, not in a frame of the motion.
            _savePending = true;
            Start(Move.Close, State.Closing);
        }
        else if (Now == State.Opening)
        {
            _main.Audio.SetPauseMusic(true);
            Start(Move.Close, State.Closing);
        }
    }

    public void Resume()
    {
        if (Now is State.Playing or State.Opening or State.Settings) return;
        _main.Audio.SetPauseMusic(false);
        var rig = _main.Rig;
        if (_followAfter) { rig.UserMoved = false; _main.Follow(instant: false); }
        else { rig.LookAt(_keptPivot, _keptSize, instant: false); rig.UserMoved = _keptMoved; }
        Start(Move.Open, State.Opening);
    }

    /// <summary>While the book is closed, a load or a new town sets where Resume goes (moved false: follow the town).</summary>
    public void KeepView(Vector3 pivot, float size, bool moved)
    {
        _keptPivot = pivot; _keptSize = size; _keptMoved = moved;
        _followAfter = !moved;
    }

    /// <summary>Where the camera was when the book closed (what Resume returns to): pivot, view height, moved by hand.</summary>
    public (Vector3 pivot, float size, bool moved) Kept => (_keptPivot, _keptSize, _keptMoved);

    public void OpenSettings()
    {
        // Also from the lettering while it is still coming in (the board has landed by then): from where everything is.
        if (Now is not (State.Cover or State.Closing or State.Returning)) return;
        _pages.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Inherited;
        _pages.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Inherited;
        _pages.Visible = true;
        _pages.Live = true;
        _pages.OpenFresh();
        Start(Move.ToSettings, State.Settings);
    }

    private void CloseSettings()
    {
        if (Now != State.Settings) return;
        // The spread goes on fading out but takes no more keys or clicks.
        _pages.Live = false;
        _pages.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Disabled;
        _pages.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
        Start(Move.FromSettings, State.Returning);
    }

    private void Ask()
    {
        if (Now is not (State.Cover or State.Closing or State.Returning)) return;
        Start(Move.AskIn, State.Asking);
    }

    private void CancelAsk()
    {
        if (Now != State.Asking) return;
        Start(Move.AskOut, State.Cover);
    }

    private void ConfirmNewTown()
    {
        if (Now != State.Asking) return;
        _main.NewTown();
        _followAfter = true;
        Resume();
    }

    private void SaveAndQuit() => _main.QuitGame();

    // ------------------------------------------------------------------ staging

    /// <summary>Starts a transition from wherever every part is now.</summary>
    private void Start(Move move, State now)
    {
        bool turning = _motion.Running;   // taking over from another transition: its parts start at once
        KeepOld(turning);
        _move = move;
        Now = now;
        var rig = _main.Rig;
        _hudFrom = _hudA; _letFrom = _letA; _askFrom = _askA; _pagFrom = _pagA; _covFrom = _main.Cover.Amount; _sinkFrom = _main.Cover.Sink;
        _camFromPivot = rig.PivotLocal; _camFromSize = rig.Camera.Size;
        _focusOnLetters = null;
        float len;
        // Defaults: a part not named below fades out quickly at the start.
        _hudTo = 0; _hud0 = 0; _hud1 = 0.2f;
        _letTo = 0; _let0 = 0; _let1 = 0.2f;
        _askTo = 0; _ask0 = 0; _ask1 = 0.2f;
        _pagTo = 0; _pag0 = 0; _pag1 = 0.2f;
        _covTo = _covFrom; _cov0 = 0; _cov1 = 0; _covLand = false;
        _camMoves = false; _cam0 = 0; _cam1 = 0;
        float down = SwingDown * Math.Max(0.4f, 1f - _covFrom), up = SwingUp * Math.Max(0.4f, _covFrom);
        // A board still swinging turns round over a time of its own, so its carried speed gives way gently.
        if (turning && _vel[PCov] != 0f) { down = Math.Max(down, CarryBoard); up = Math.Max(up, CarryBoard); }
        switch (move)
        {
            case Move.Close:
            {
                var (c, size) = _main.ClosedBookView();
                Camera(c, size, 0f, turning ? 0.7f : 0.8f);
                _cov0 = turning ? 0f : 0.35f;
                Board(1f, _cov0, _cov0 + down, land: true);
                _let0 = _touch + 0.04f; _let1 = _let0 + 0.3f; _letTo = 1f; _focusOnLetters = _resume;
                len = _let1;
                break;
            }
            case Move.Open:
                _cov0 = turning ? 0f : 0.08f;
                Board(0f, _cov0, _cov0 + up, land: false);
                // Taken over with the camera still at rest (Esc pressed on frames running), it waits as from rest: a move
                // started and turned round within a frame or two would start at a jolt against its tiny top speed.
                bool camGoing = turning && CamGoing;
                Camera(rig.TargetPivot, rig.TargetSize, camGoing ? 0f : 0.2f, camGoing ? 0.7f : 0.9f);
                _hudTo = 1f; _hud0 = turning ? 0.5f : 0.7f; _hud1 = _hud0 + 0.25f;
                len = Math.Max(_hud1, _cam1);
                rig.SetDeep(true);
                break;
            case Move.ToSettings:
            {
                // The board up and the camera at rest over the spread first; then a leaf turns from the town's pages to
                // the settings pages (the 2D pages are fixed on screen: never revealed over a book still drifting).
                _cov0 = turning ? 0f : 0.03f;
                Board(0f, _cov0, _cov0 + up, land: false);
                var (c, size) = SpreadView();
                Camera(c, size, turning ? 0f : 0.05f, turning ? 0.6f : 0.65f);
                _pagTo = 1f;
                // (A hair after both, so even at 30 frames a second no frame both moves the camera and the leaf.)
                _pag0 = Math.Max(_camMoves ? _cam1 + 0.034f : 0f, _covTo != _covFrom ? _cov1 + 0.034f : 0f);
                _pag1 = _pag0 + Math.Max(0.12f, LeafSeconds * (1f - _pagFrom));
                len = _pag1;
                break;
            }
            case Move.FromSettings:
            {
                // The leaf turns back to the town's pages; only then do the camera and the board move.
                // A tab's page still turning finishes first (the two leaves never move at once).
                _pag0 = _pagFrom > 0f ? _pages.TurnLeft : 0f;
                _pag1 = _pagFrom > 0f ? _pag0 + Math.Max(0.1f, LeafBackSeconds * _pagFrom) : 0f;
                var (c, size) = _main.ClosedBookView();
                Camera(c, size, _pag1 > 0f ? _pag1 + 0.034f : 0f, (_pag1 > 0f ? _pag1 + 0.034f : 0f) + (turning ? 0.55f : 0.5f));
                _cov0 = _cam0 + 0.02f;
                Board(1f, _cov0, _cov0 + down, land: true);
                _let0 = _touch + 0.04f; _let1 = _let0 + 0.25f; _letTo = 1f; _focusOnLetters = _settings;
                len = _let1;
                break;
            }
            case Move.AskIn:
            {
                // One after the other, never both showing: the lettering goes, then the question comes.
                _let1 = AskOutEnd;
                _askTo = 1f; _ask0 = AskInStart; _ask1 = AskSeconds;
                len = AskSeconds;
                // Asked for while the board was still settling: it and the camera finish on the way.
                var (c, size) = _main.ClosedBookView();
                Camera(c, size, 0f, AskSeconds);
                if (_covFrom < 1f) Board(1f, 0f, AskSeconds, land: false);
                break;
            }
            default: // AskOut
                _ask1 = AskOutEnd;
                _letTo = 1f; _let0 = AskInStart; _let1 = AskSeconds;
                len = AskSeconds;
                break;
        }
        if (move is Move.Close or Move.Open or Move.ToSettings or Move.FromSettings)
        {
            rig.SetDeep(true);
            // From rest, the frame that took the input is the frame before the motion's first: the left page is written
            // now, so its text never changes while it turns with the board; the clock starts in the next frame. (Taking
            // over from another transition, the page was written when that one started and the motion goes straight on.)
            if (!turning) { _main.Summary.UpdateNow(); _prep = true; }
        }
        PlanTails(move);
        if (_letTail.On) len = Math.Max(len, _let1);
        if (_askTail.On) len = Math.Max(len, _ask1);
        PlanCarry(turning, len);
        DropTailCarry();
        _main.Cover.Driven = BoardMoves;
        _motion.Start(len);
        // Parts that fade in from their first moment are made visible now, at no opacity: the question takes the focus
        // at once, and so does the cover when it comes back from the question.
        if (_focusOnLetters != null && _cover.Visible) { _focusOnLetters.GrabFocus(); _focusOnLetters = null; }
        if (move == Move.AskIn) { _ask.Visible = true; _no.GrabFocus(); }
        if (move == Move.AskOut) { _cover.Visible = true; _newTown.GrabFocus(); }
        Apply();
    }

    private void Camera(Vector3 pivot, float size, float t0, float t1)
    {
        _camToPivot = pivot; _camToSize = size; _cam0 = t0; _cam1 = t1;
        _camMoves = _camFromPivot.DistanceTo(pivot) > 1e-3f || MathF.Abs(_camFromSize - size) > 1e-3f;
    }

    private void Board(float to, float t0, float t1, bool land)
    {
        _covTo = to; _cov0 = t0; _cov1 = t1; _covLand = land;
        _touch = land ? t0 + (t1 - t0) * Touch : t1;
        if (_covFrom == to) { _cov1 = t0; _touch = t0; }
        // The town's models: down on their own eased window, ending a little before the board is low over them; up once
        // the board has lifted clear of them.
        _sinkTo = to;
        if (land) { _sink0 = Math.Max(0f, t0 - 0.05f); _sink1 = Math.Max(_sink0 + 0.2f, _touch - 0.06f); }
        else { float k = (t1 - t0) / SwingUp; _sink0 = t0 + 0.12f * k; _sink1 = _sink0 + 0.45f * k; }
    }

    private bool _prep;
    /// <summary>The frame before the motion's first (the frame that took the input) is under way (tests).</summary>
    public bool Preparing => _prep;

    private void Step(double delta)
    {
        if (_prep) { _prep = false; Apply(); return; }
        bool ended = _motion.Advance(delta);
        Apply();
        if (ended) Rest();
    }

    /// <summary>Ends the transition under way at once, in its end state (impatient input that cannot turn it round).</summary>
    public void FinishNow()
    {
        if (!_motion.Running) return;
        _prep = false;
        _motion.Release();
        _motion.Finish();
        Step(0);
    }

    /// <summary>Every part at its place for the timeline's time now.</summary>
    private void Apply()
    {
        var m = _motion;
        // Sounds: the board opening as it starts to move, closing as it touches.
        if (_covTo != _covFrom)
        {
            if (_covTo < _covFrom && m.Passed(Math.Max(0.001f, _cov0))) _main.Audio.BookOpen();
            if (_covTo > _covFrom && m.Passed(Math.Max(0.001f, _touch))) _main.Audio.BookClose();
        }
        // Each part on its eased curve, plus the speed it carried from a transition taken over (PauseMenu.Carry.cs).
        _hudA = Clamp01(BookMotion.Mix(_hudFrom, _hudTo, m.Ease(_hud0, _hud1)) + CarryOf(PHud));
        _letA = _letTail.Covers(m.Time) ? _letTail.At(m) : Clamp01(BookMotion.Mix(_letFrom, _letTo, m.Ease(_let0, _let1)) + CarryOf(PLet));
        _askA = _askTail.Covers(m.Time) ? _askTail.At(m) : Clamp01(BookMotion.Mix(_askFrom, _askTo, m.Ease(_ask0, _ask1)) + CarryOf(PAsk));
        _pagA = Clamp01(BookMotion.Mix(_pagFrom, _pagTo, m.Ease(_pag0, _pag1)) + CarryOf(PPag));
        _askSlide = 1f - _askA;
        var hud = _main.Hud;
        hud.Fade = _hudA;
        if (_hudTo > 0 && !hud.Visible && (m.Time >= _hud0 || _hudA > 0f)) hud.Visible = true;
        if (_hudTo <= 0 && _hudA <= 0f && hud.Visible)
        {
            hud.Visible = false;
            // Pages of the interface close once it has faded out (quietly: the book has its own sounds).
            hud.CloseAchievements(quiet: true);
            hud.CloseAgePage(quiet: true);
        }
        // The gilt brightens a little as the lettering comes in.
        float glow = _letTo > _letFrom ? 0.22f * MathF.Sin(MathF.PI * m.Linear(_let0, _let1)) : 0f;
        _cover.Modulate = new Color(1 + glow, 1 + glow * 0.9f, 1 + glow * 0.6f, _letA);
        if (_letTo > 0 && !_cover.Visible && (m.Time >= _let0 || _letA > 0f))
        {
            _cover.Visible = true;
            _focusOnLetters?.GrabFocus();
            _focusOnLetters = null;
        }
        if (_letTo <= 0 && _letA <= 0f && _cover.Visible) _cover.Visible = false;
        _ask.Modulate = new Color(1, 1, 1, _askA);
        if (_askTo > 0 && !_ask.Visible && (m.Time >= _ask0 || _askA > 0f)) _ask.Visible = true;
        if (_askTo <= 0 && _askA <= 0f && _ask.Visible) _ask.Visible = false;
        // The spread is not faded: it arrives and leaves by a leaf turning (SettingsPages.SetArrival).
        if (_pages.Modulate.A != 1f && _pages.Visible) _pages.Modulate = Colors.White;
        if (_pages.Visible) _pages.SetArrival(_pagA);
        if (_pagTo <= 0 && _pagA <= 0f && _pages.Visible) _pages.Visible = false;
        if (BoardMoves)
        {
            float u = m.Linear(_cov0, _cov1);
            _covA = Clamp01(BookMotion.Mix(_covFrom, _covTo, _covLand ? BookMotion.Land(u, Touch) : BookMotion.Smoother(u)) + CarryOf(PCov));
            _sinkA = Clamp01(BookMotion.Mix(_sinkFrom, _sinkTo, m.Ease(_sink0, _sink1)) + CarryOf(PSink));
            _main.Cover.Set(_covA, _sinkA);
        }
        if (_camMoves)
        {
            float e = m.Ease(_cam0, _cam1), kv = BookMotion.Carry(1f, 0f, _cd[PSize], m.Time), ka = BookMotion.Carry(0f, 1f, _cd[PSize], m.Time);
            _camP = (e >= 1f ? _camToPivot : _camFromPivot.Lerp(_camToPivot, e)) + _cvPiv * kv + _caPiv * ka;
            _camLS = MathF.Log(Math.Max(1e-3f, BookMotion.MixSize(_camFromSize, _camToSize, e))) + CarryOf(PSize);
            _main.Rig.Place(_camP, MathF.Exp(_camLS));
        }
        Measure();
    }

    /// <summary>The transition has ended: the state it leads to, at rest.</summary>
    private void Rest()
    {
        _main.Cover.Rest();
        var rig = _main.Rig;
        switch (_move)
        {
            case Move.Close: Now = State.Cover; break;
            case Move.FromSettings: Now = State.Cover; break;
            case Move.AskIn: Now = State.Asking; break;
            case Move.AskOut: Now = State.Cover; break;
            case Move.ToSettings: Now = State.Settings; break;
            case Move.Open:
                rig.SetDeep(false);
                // The camera stays exactly where the move put it; the follow easing picks up from there.
                if (_savePending) { _savePending = false; _saveDue = true; }
                Now = State.Playing;
                _main.Driver.SetPaused(false);
                rig.InputLocked = false;
                if (GetViewport().GuiGetFocusOwner() is { } f && (f == _resume || f.GetParent() == _cover || f.GetParent() == _ask)) f.ReleaseFocus();
                return;
        }
        if (_savePending) { _savePending = false; _saveDue = true; }
    }

    /// <summary>The save is written in the frame after a transition's last (PauseMenu._Process), never in one of its frames.</summary>
    private bool _saveDue;

    private void SaveDue()
    {
        if (!_saveDue) return;
        _saveDue = false;
        SaveNow();
    }

    private void SaveNow()
    {
        _savePending = false;
        _main.Saver.SaveNow();
        MenuSaves++;
    }
}
