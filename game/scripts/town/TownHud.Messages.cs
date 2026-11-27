using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, round L2 (message-system.md sections 1 and 3): the two queues of the interface's large zones. The Message
/// zone (top centre) shows one large message at a time: banners (a level, the drive, the charter, the new age, the
/// seal) and trouble (a save that failed, brick border, until clicked); the choice of two takes the zone at once and
/// banners wait while it, a page or the zoom holds the zone (one that has waited 12 s becomes a note). The ledger's note
/// section shows one note at a time (two lines, 90 characters at most). Both arrive and leave by BookMotion timelines
/// (large: 0.22 s, fade and 12 down from above; notes: 0.2 s, fade and 8 in from the left), 0.15 s apart; both can be
/// clicked away. A message's sound plays when it appears.
/// </summary>
public partial class TownHud
{
    public const float LargeSeconds = 0.22f, NoteInSeconds = 0.2f, MessageGap = 0.15f;
    public const float BannerStay = 3.2f, BannerLongStay = 5f, BannerCrowdStay = 2.2f, BannerWaitLimit = 12f;
    public const float NoteStay = 3.2f, NoteCrowdStay = 2f;
    public const int NoteCrowdAt = 4, NoteCap = 6, NoteMaxChars = 90;
    /// <summary>Priorities (message-system section 1).</summary>
    public const int PrioTrouble = 4, PrioMoment = 3, PrioDrive = 2, PrioLevel = 1;
    public const int NoteAchievement = 3, NoteBuilding = 2, NoteOther = 1;
    private const int KindTrouble = 1, KindLevel = 2, KindDrive = 3, KindAge = 4;

    private readonly MessageQueue _large = new()
    {
        InSeconds = LargeSeconds, OutSeconds = LargeSeconds, GapSeconds = MessageGap, CrowdAt = 2, CrowdStay = BannerCrowdStay,
        Cap = 6, WaitLimit = BannerWaitLimit,
    };
    private readonly MessageQueue _notes = new()
    {
        InSeconds = NoteInSeconds, OutSeconds = NoteInSeconds, GapSeconds = MessageGap, CrowdAt = NoteCrowdAt, CrowdStay = NoteCrowdStay, Cap = NoteCap,
    };
    private int _largeSeq = -1, _noteSeq = -1;
    private float _msgDt;
    /// <summary>Frame strips only: when 0 or more, the interface's clocks step by this much a frame (0 holds them).</summary>
    public static float StripStep = -1f;
    /// <summary>Frame strips only: the one step the next interface frame takes while held (then 0 again).</summary>
    public static float StripNext;
    private bool _queuesWired;

    /// <summary>The queues (tests: what shows, what waits, the phase and its eased opacity).</summary>
    public MessageQueue LargeQueue => _large;
    public MessageQueue NoteQueue => _notes;
    /// <summary>The large zone may show a banner now: no choice, no page, the full interface in view.</summary>
    public bool MessageZoneFree => !_drv.Town.ChoiceOpen && _choiceF.Value <= 0f && !AchievementsOpen && !AgePageOpen && _fullA >= 0.5f && !PageTurning; // (a follow view may stand part-way into the fade)
    /// <summary>What is on screen this frame (tests: Visible may change only at no opacity).</summary>
    public bool BannerShown => _banner.Visible;
    public Rect2 NoteRect => _noteLabel.Visible ? _noteLabel.GetGlobalRect() : new Rect2();
    public Rect2 ChoiceRect => _choice.Visible ? _choice.GetGlobalRect() : new Rect2();
    public Rect2 RequestContentRect => _req.Visible ? _req.GetGlobalRect() : new Rect2();
    public bool NoteLabelShown => _noteLabel.Visible;
    public bool LineCardShown => _lineCard.Visible;

    private void WireQueues()
    {
        if (_queuesWired) return;
        _queuesWired = true;
        // A banner that could not wait is said as a note instead (message-system section 1).
        _large.Overflow = it => { if (!it.UntilClicked) QueueNote(it.Head + ": " + it.Text, true, NoteOther, ""); };
        _large.Shown = it => { PlayFor(it.Sound); if (it.Kind == KindLevel) FirstLevelSeen(); };
        // L2 review: a banner is checked against the town when it is about to show (and while it shows): a level the
        // town has left, a drive that is over, a new age the town is already a level into, are no longer true.
        _large.Stale = it => it.Kind switch
        {
            KindLevel or KindAge => it.About < _drv.Town.Level,
            KindDrive => !_drv.Town.Drive.Active,
            _ => false,
        };
        _notes.Shown = it => PlayFor(it.Sound);
    }

    private static bool IsLargeSound(string id) => id is "level-reached" or "charter-granted" or "gift-arrives" or "request-met" or "seal-granted";

    private void PlayFor(string id)
    {
        if (id.Length == 0 || _main?.Audio == null) return;
        if (IsLargeSound(id)) _main.Audio.Large(id); else _main.Audio.Moment(id);
    }

    /// <summary>A banner of the lowest priority (a level's kind), 3.2 s (the old entry point; tests use it).</summary>
    public void Banner(string head, string text) => Large(head, text, PrioLevel, BannerStay, "");

    /// <summary>A large message in the Message zone: it waits its turn by priority.</summary>
    public void Large(string head, string text, int priority, float stay, string sound, bool trouble = false)
    {
        WireQueues();
        string line = trouble ? text : head + ": " + text;
        Log(BannerLog, line);
        Log(NoteLog, line);
        LastNote = line;
        _bannerAt = null;
        // A level banner still waiting is old news once the next level comes: the newest level speaks for both.
        int kind = trouble ? KindTrouble : head.StartsWith("Level ") ? KindLevel : head == Town.DriveName ? KindDrive : head == AgeBannerHead ? KindAge : 0;
        int about = _drv.Town.Level;
        if (kind == KindLevel && int.TryParse(head.AsSpan(6), out int lv)) about = lv;
        if (kind == KindLevel) _large.DropWaiting(KindLevel);
        _large.Push(head, text, priority, trouble ? 0f : stay, sound, false, kind, about);
    }

    public const string AgeBannerHead = "A new age";

    /// <summary>Trouble (a save that failed or could not be read): the Message zone, brick border, until clicked.</summary>
    public void Trouble(string text) => Large("Something went wrong", text, PrioTrouble, 0f, "", trouble: true);

    /// <summary>A note (priority 1); strong notes are set bold.</summary>
    public void Note(string text, bool strong = false) => Note(text, strong, NoteOther, "");

    public void Note(string text, bool strong, int priority, string sound)
    {
        NotesShown++;
        LastNote = text;
        Log(NoteLog, text);
        QueueNote(text, strong, priority, sound);
    }

    private void QueueNote(string text, bool strong, int priority, string sound)
    {
        WireQueues();
        if (text.Length > NoteMaxChars) text = text.Substring(0, NoteMaxChars - 1).TrimEnd() + "…";
        _notes.Push("", text, priority, NoteStay, sound, strong);
    }

    /// <summary>A new town: nothing of the old one shows or waits.</summary>
    private void ClearMessages() { _large.Clear(); _notes.Clear(); _largeSeq = _noteSeq = -1; _refusal = ""; }
    /// <summary>Frame strips and pictures: the zones emptied at once (what a new town does).</summary>
    public void ClearMessagesNow() => ClearMessages();

    /// <summary>One frame of both queues and the controls that show them (after the zones are placed).</summary>
    private void MessagesFrame(float dt)
    {
        WireQueues();
        _large.Step(dt, MessageZoneFree);
        _notes.Step(dt, _fullA > 0.01f);
        var z = Zones;
        // The banner: fade and 12 down from above.
        if (_large.Showing && _large.Current.Seq != _largeSeq)
        {
            var c = _large.Current;
            _largeSeq = c.Seq;
            _bannerHead.Text = c.Head;
            _bannerText.Text = c.Text;
            _banner.AddThemeStyleboxOverride("panel", c.Kind == KindTrouble ? _troubleBox : _accentBox);
            _bannerAge = 0;
        }
        float a = _large.Showing ? _large.Alpha : 0f;
        bool on = a > 0f;
        if (_banner.Visible != on) _banner.Visible = on;
        if (on)
        {
            // The card stays in its zone (a zone never moves) and fades; its words come 12 down from above.
            _banner.Position = z.Banner.Position;
            _banner.Size = z.Banner.Size;
            float dy = -UiKit.U(_s, 8) * (1f - a); // (8, not 12: the words stay inside the card's zone)
            _bannerHead.Position = new Vector2(_bannerHead.Position.X, UiKit.U(_s, 8) + dy);
            _bannerText.Position = new Vector2(_bannerText.Position.X, UiKit.U(_s, 40) + dy);
            _banner.Modulate = new Color(1, 1, 1, a * Math.Max(_fullA, _large.Current.Kind == KindTrouble ? 1f : 0f));
        }
        // The note: fade and 8 in from the left.
        if (_notes.Showing && _notes.Current.Seq != _noteSeq)
        {
            var c = _notes.Current;
            _noteSeq = c.Seq;
            _noteLabel.AddThemeFontOverride("font", c.Strong ? UiKit.UiBold : UiKit.UiFont);
            // An achievement's note carries its medal at the left of the text (the text moves over by the medal).
            var medal = c.Priority == NoteAchievement ? NoteMedal(c.Text) : null;
            _noteIcon.Texture = medal;
            _noteShift = medal != null ? UiKit.U(_s, 24 + 8) : 0;
            _noteLabel.Size = new Vector2(_noteW - _noteShift, _noteLabel.Size.Y);
            _noteLabel.Text = FitLines(_noteLabel, c.Text, 2);
        }
        float na = _notes.Showing ? _notes.Alpha : 0f;
        bool non = na > 0f;
        if (_noteLabel.Visible != non) _noteLabel.Visible = non;
        bool ion = non && _noteShift > 0;
        if (_noteIcon.Visible != ion) _noteIcon.Visible = ion;
        if (non)
        {
            float x = _notePad - UiKit.U(_s, 8) * (1f - na);
            _noteLabel.Position = new Vector2(x + _noteShift, _noteLabel.Position.Y);
            _noteLabel.Modulate = new Color(1, 1, 1, na);
            if (ion) { _noteIcon.Position = new Vector2(x, _noteLabel.Position.Y + UiKit.U(_s, 2)); _noteIcon.Modulate = _noteLabel.Modulate; }
        }
        _noteAge = _notes.Showing ? 0f : -1f;
    }

    /// <summary>A note's text as the note section shows it (two lines at most; tests).</summary>
    public string NoteFit(string text) => FitLines(_noteLabel, text, 2);

    /// <summary>The text cut at a word, with an ellipsis, so that it takes at most <paramref name="lines"/> lines of the label.</summary>
    private static string FitLines(Label l, string text, int lines)
    {
        var f = l.GetThemeFont(FontName);
        int px = l.GetThemeFontSize(FontSizeName);
        float w = l.Size.X - 2f, lh = f.GetHeight(px);
        bool Fits(string s) => f.GetMultilineStringSize(s, HorizontalAlignment.Left, w, px).Y <= lh * lines + 1f;
        if (w <= 0 || Fits(text)) return text;
        string t = text;
        while (t.Length > 8)
        {
            int cut = t.LastIndexOf(' ');
            if (cut <= 0) break;
            t = t.Substring(0, cut).TrimEnd(',', '.', ';', ':');
            if (Fits(t + "\u2026")) return t + "\u2026";
        }
        return t;
    }

    private StyleBox _accentBox = null!, _troubleBox = null!;
    private float _notePad, _noteW, _noteShift;
    private TextureRect _noteIcon = null!;

    /// <summary>The medal of the achievement a note "Achievement: Name." is about (either age), or null.</summary>
    private Texture2D? NoteMedal(string text)
    {
        var t = _drv.Town;
        for (int i = 0; i < t.AchievementCount; i++) { var a = t.Achievement(i); if (text == "Achievement: " + a.Name + ".") return AchIcon(a, false); }
        for (int i = 0; i < t.RiverAchievementCount; i++) { var a = t.RiverAchievement(i); if (text == "Achievement: " + a.Name + ".") return AchIcon(a, true); }
        return null;
    }

    /// <summary>Banners and notes are clicked away; the next one follows.</summary>
    private void WireMessageClicks()
    {
        _accentBox = UiKit.CardBox(_s, UiKit.CardKind.Accent, 0);
        _troubleBox = UiKit.CardBox(_s, UiKit.CardKind.Trouble, 0);
        _banner.MouseFilter = Control.MouseFilterEnum.Stop;
        _banner.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { _large.Dismiss(); _banner.AcceptEvent(); } };
        _noteBox.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _notes.Showing) { _notes.Dismiss(); _noteBox.AcceptEvent(); } };
        _notePad = _noteLabel.Position.X;
        _noteW = _noteLabel.Size.X;
        _noteShift = 0f;
        _noteIcon = UiKit.IconRect(_s, null, 24);
        _noteIcon.Name = "NoteMedal";
        _noteIcon.Visible = false;
        _noteBox.AddChild(_noteIcon);
        _largeSeq = _noteSeq = -1;
    }
}
