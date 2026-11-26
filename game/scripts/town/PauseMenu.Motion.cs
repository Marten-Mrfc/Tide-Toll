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

    // Lengths (seconds) of each transition when it starts from rest.
    public const float CloseSeconds = 1.25f, OpenSeconds = 0.95f, ToSettingsSeconds = 0.9f, FromSettingsSeconds = 1.05f,
        AskSeconds = 0.3f;
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
                Camera(rig.TargetPivot, rig.TargetSize, turning ? 0f : 0.2f, turning ? 0.7f : 0.9f);
                _hudTo = 1f; _hud0 = turning ? 0.5f : 0.7f; _hud1 = _hud0 + 0.25f;
                len = Math.Max(_hud1, _cam1);
                rig.SetDeep(true);
                break;
            case Move.ToSettings:
            {
                _cov0 = turning ? 0f : 0.08f;
                Board(0f, _cov0, _cov0 + up, land: false);
                var (c, size) = SpreadView();
                Camera(c, size, turning ? 0f : 0.1f, turning ? 0.6f : 0.7f);
                _pagTo = 1f; _pag0 = turning ? 0.35f : 0.5f; _pag1 = _pag0 + 0.4f;
                len = _pag1;
                break;
            }
            case Move.FromSettings:
            {
                _pag1 = turning ? 0.2f : 0.25f;
                var (c, size) = _main.ClosedBookView();
                Camera(c, size, turning ? 0f : 0.1f, turning ? 0.55f : 0.65f);
                _cov0 = turning ? 0f : 0.15f;
                Board(1f, _cov0, _cov0 + down, land: true);
                _let0 = _touch + 0.04f; _let1 = _let0 + 0.3f; _letTo = 1f; _focusOnLetters = _settings;
                len = _let1;
                break;
            }
            case Move.AskIn:
            {
                _let1 = 0.2f;
                _askTo = 1f; _ask0 = 0.05f; _ask1 = AskSeconds;
                len = AskSeconds;
                // Asked for while the board was still settling: it and the camera finish on the way.
                var (c, size) = _main.ClosedBookView();
                Camera(c, size, 0f, AskSeconds);
                if (_covFrom < 1f) Board(1f, 0f, AskSeconds, land: false);
                break;
            }
            default: // AskOut
                _ask1 = 0.2f;
                _letTo = 1f; _let0 = 0f; _let1 = AskSeconds;
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
        _main.Cover.Driven = _covTo != _covFrom;
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
        _hudA = BookMotion.Mix(_hudFrom, _hudTo, m.Ease(_hud0, _hud1));
        _letA = BookMotion.Mix(_letFrom, _letTo, m.Ease(_let0, _let1));
        _askA = BookMotion.Mix(_askFrom, _askTo, m.Ease(_ask0, _ask1));
        _pagA = BookMotion.Mix(_pagFrom, _pagTo, m.Ease(_pag0, _pag1));
        _askSlide = 1f - _askA;
        var hud = _main.Hud;
        hud.Fade = _hudA;
        if (_hudTo > 0 && !hud.Visible && m.Time >= _hud0) hud.Visible = true;
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
        if (_letTo > 0 && !_cover.Visible && m.Time >= _let0)
        {
            _cover.Visible = true;
            _focusOnLetters?.GrabFocus();
            _focusOnLetters = null;
        }
        if (_letTo <= 0 && _letA <= 0f && _cover.Visible) _cover.Visible = false;
        _ask.Modulate = new Color(1, 1, 1, _askA);
        if (_askTo > 0 && !_ask.Visible && m.Time >= _ask0) _ask.Visible = true;
        if (_askTo <= 0 && _askA <= 0f && _ask.Visible) _ask.Visible = false;
        _pages.Modulate = new Color(1, 1, 1, _pagA);
        if (_pagTo <= 0 && _pagA <= 0f && _pages.Visible) _pages.Visible = false;
        if (_covTo != _covFrom)
        {
            float u = m.Linear(_cov0, _cov1);
            _main.Cover.Set(BookMotion.Mix(_covFrom, _covTo, _covLand ? BookMotion.Land(u, Touch) : BookMotion.Smoother(u)),
                BookMotion.Mix(_sinkFrom, _sinkTo, m.Ease(_sink0, _sink1)));
        }
        if (_camMoves)
        {
            float e = m.Ease(_cam0, _cam1);
            _main.Rig.Place(e >= 1f ? _camToPivot : _camFromPivot.Lerp(_camToPivot, e), BookMotion.MixSize(_camFromSize, _camToSize, e));
        }
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
