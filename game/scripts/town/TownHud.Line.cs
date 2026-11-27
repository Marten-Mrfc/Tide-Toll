using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3, round L2 (message-system.md section 1, the Line): the Line zone says one thing, in this order of priority:
/// the answer to a click that could not be done (2.5 s), what the click mode in use expects, a first-minute hint
/// (TownHud.FirstMinute.cs), the town's hint (with its ring on the map). The card arrives and leaves over 0.2 s; a
/// change of text is a cross-fade of two labels of one fixed size over 0.2 s (the card never changes size).
/// </summary>
public partial class TownHud
{
    public const float RefusalSeconds = 2.5f, LineSeconds = 0.2f;
    public enum LineKind { None, Refusal, Mode, FirstHint, TownHint }

    private Label _lineBack = null!;
    private readonly MessageFader _lineCardF = new(LineSeconds);
    private readonly BookMotion _lineCross = new();
    private string _refusal = "";
    private float _refusalAge;

    /// <summary>What the line says now (tests).</summary>
    public LineKind LineKindNow { get; private set; }
    /// <summary>The line's opacity (the card) and the cross-fade's progress, 1 at rest (tests).</summary>
    public float LineAlpha => _lineCardF.Value;
    public float LineCross => _lineCross.Running ? _lineCross.Ease(0f, _lineCross.Length) : 1f;
    public string RefusalText => _refusal;
    public bool LineCrossing => _lineCross.Running;

    /// <summary>A click that could not be done: the line says why for 2.5 s.</summary>
    public void Refuse(string text)
    {
        LastNote = text;
        Log(NoteLog, text);
        _refusal = text;
        _refusalAge = 0f;
    }

    private void LineFrame()
    {
        float dt = _msgDt;
        if (_refusal.Length > 0) { _refusalAge += MessageQueue.Clock(dt); if (_refusalAge >= RefusalSeconds) _refusal = ""; }
        string want; LineKind kind;
        if (_refusal.Length > 0) { want = _refusal; kind = LineKind.Refusal; }
        else if (PaveMode || ExtendMode) { want = ModeHelp(PaveMode); kind = LineKind.Mode; }
        // A first-minute hint is held out of the line while a decision or a page is open (it waits; its time does not run).
        else if (_firstHint.Length > 0 && !(_drv.Town.ChoiceOpen || AchievementsOpen || AgePageOpen)) { want = _firstHint; kind = LineKind.FirstHint; }
        else if (_firstHint.Length > 0) { want = ""; kind = LineKind.None; }
        else if (_hintShown.Length > 0) { want = _hintShown; kind = LineKind.TownHint; }
        else { want = ""; kind = LineKind.None; }
        LineKindNow = kind;
        bool show = want.Length > 0 && _fullA > 0.01f;
        _lineCardF.Want(show);
        _lineCardF.Step(dt);
        // (Compared with the C# string last set: reading Label.Text makes a new string every frame.)
        if (want.Length > 0 && !ReferenceEquals(want, _lineShown) && want != _lineShown)
        {
            if (_lineCardF.Value > 0f && _lineShown.Length > 0)
            {
                // A cross-fade: the old text fades out under the new one.
                _lineBack.Text = _lineShown;
                _lineCross.Start(LineSeconds);
            }
            _lineShown = want;
            _lineText.Text = want;
            FitLine();
        }
        if (_lineCross.Running) _lineCross.Advance(dt);
        float x = LineCross;
        _lineText.Modulate = new Color(1, 1, 1, x);
        _lineBack.Modulate = new Color(1, 1, 1, 1f - x);
        float a = _lineCardF.Value;
        bool vis = a > 0f;
        if (_lineCard.Visible != vis) _lineCard.Visible = vis;
        if (vis)
        {
            _lineCard.Modulate = new Color(1, 1, 1, a * Math.Clamp(_fullA, 0f, 1f));
        }
    }

    /// <summary>
    /// Fix round (n7 trades): the line holds its text on every canvas. One line when it fits; else two, the card growing
    /// upward from its zone's bottom edge (never cut, never into the middle). Measured only when the text changes; the
    /// old text of a cross-fade is measured too, so the card is as tall as the taller of the two.
    /// </summary>
    /// <summary>Tests (the layout fixture): the line sized for this text as if it showed it.</summary>
    public void FitLineFor(string text) { _lineShown = text; _lineText.Text = text; FitLine(); }

    private void FitLine()
    {
        var z = Zones;
        float w = _lineText.Size.X, zoneH = z.Line.Size.Y;
        int px = _lineText.GetThemeFontSize("font_size");
        Font f = UiKit.CapFont;
        float h = Math.Max(TextHeight(f, _lineShown, w, px), _lineCross.Running ? TextHeight(f, _lineBack.Text, w, px) : 0f);
        float cardH = Math.Max(zoneH, MathF.Ceiling(h + UiKit.U(_s, 6)));
        var r = new Rect2(z.Line.Position.X, z.Line.End.Y - cardH, z.Line.Size.X, cardH);
        if (_lineCard.Position != r.Position || _lineCard.Size != r.Size)
        {
            _lineCard.Position = r.Position;
            _lineCard.Size = r.Size;
            _lineText.Size = new Vector2(w, cardH);
            _lineBack.Size = _lineText.Size;
        }
    }

    private static float TextHeight(Font f, string text, float w, int px) =>
        text.Length == 0 ? 0f : f.GetMultilineStringSize(text, HorizontalAlignment.Center, w, px, -1, TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Mandatory).Y;
}
