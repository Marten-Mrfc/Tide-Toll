using System;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game;

/// <summary>
/// Pausing closes the book and the closed cover is the menu. tt_pause (Esc) or the interface's pause mark: the
/// interface fades, the camera pulls back to the book, the front board swings shut (BookCover, book-close sound as it
/// lands), the simulation stops, the pause piece plays and the town is saved once the book is still. On the cover, in
/// gilt: the title, the town's name, and Resume, Settings, New town, Quit (mouse, or up, down and Enter; Esc resumes).
/// Resume opens the book and puts the camera back where it was. Settings opens the book at the settings pages
/// (SettingsPages, built once at start-up and kept); Back returns to the cover. New town asks once. Quit saves and quits.
/// Every change is one staged transition (PauseMenu.Motion.cs, BookMotion): nothing on screen switches on or off.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public static readonly Color Gilt = Color.FromHtml("#D9B45A");
    public static readonly Color GiltBright = Color.FromHtml("#F4D98C");
    public static readonly Color GiltDim = Color.FromHtml("#A8873F");
    public const string Question = "Start a new town? This town will be lost.";

    /// <summary>
    /// Playing; Closing (the book closing for the menu); Cover (the menu); Settings (the settings spread, from the
    /// moment it was chosen); Asking (the New town question); Opening (the book opening to play on); Returning (from
    /// the settings back to the cover).
    /// </summary>
    public enum State { Playing, Closing, Cover, Settings, Asking, Opening, Returning }
    public State Now { get; private set; } = State.Playing;
    public bool IsPaused => Now != State.Playing;
    /// <summary>The settings pages while they are the menu's page (null otherwise; they are kept, hidden, meanwhile).</summary>
    public SettingsPages? Pages => Now == State.Settings ? _pages : null;
    /// <summary>The settings pages, whether on show or put away (tests and frame strips).</summary>
    public SettingsPages SettingsSpread => _pages;

    private Main _main = null!;
    private Control _root = null!, _cover = null!, _ask = null!;
    private Label _title = null!, _name = null!, _askText = null!;
    private Button _resume = null!, _settings = null!, _newTown = null!, _quit = null!, _yes = null!, _no = null!;
    private SettingsPages _pages = null!;
    private Button[] _coverItems = null!, _askItems = null!, _allItems = null!;
    private Vector3 _keptPivot;
    private float _keptSize;
    private bool _keptMoved, _followAfter;
    private float _s = -1;
    private Rect2 _laidRect;
    private float _laidSlide = float.NaN;
    private int _warm = 2;

    public Button ResumeButton => _resume;
    public Button SettingsButton => _settings;
    public Button NewTownButton => _newTown;
    public Button QuitButton => _quit;
    public Button YesButton => _yes;
    public Button NoButton => _no;
    /// <summary>The cover menu is the menu now (its lettering may still be fading in).</summary>
    public bool CoverShown => Now == State.Cover && _cover.Visible && _warm <= 0 && !_saveDue;
    public bool QuestionShown => _ask.Visible && Now == State.Asking;
    public string QuestionText => QuestionTitle + " " + _askText.Text;
    public const string QuestionTitle = "Start a new town?", QuestionBody = "This town will be lost.";
    private ColorRect _askScrim = null!;
    private PanelContainer? _askCard;
    private CanvasGroup _askGroup = null!;
    /// <summary>The question's card on screen (tests and the tour).</summary>
    public Rect2 QuestionRect => _askCard?.GetGlobalRect() ?? default;
    /// <summary>The lettering (title, name, menu) as drawn: Visible and its opacity (tests).</summary>
    public bool LetteringVisible => _cover.Visible;
    public bool QuestionVisible => _ask.Visible;

    public void Init(Main main)
    {
        _main = main;
        Name = "PauseMenu";
        Layer = 20;
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _cover = new Control { Name = "Lettering", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _root.AddChild(_cover);
        _cover.Draw += () => DrawTooling(_cover);
        _title = Letter("Tide & Toll", UiKit.Display, Gilt);
        _name = Letter("", UiKit.Serif, GiltDim);
        _resume = Item("Resume", Resume);
        _settings = Item("Settings", OpenSettings);
        _newTown = Item("New town", Ask);
        _quit = Item("Quit", SaveAndQuit);
        _ask = new Control { Name = "Question", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddChild(_ask);
        // Round L3: the question is the one modal card (ModalCard), centred on the window over a dimmed cover.
        _askScrim = new ColorRect { Name = "Scrim", Color = new Color(UiKit.Ink, 0.35f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ask.AddChild(_askScrim);
        // Round L3 review: the card (paper, text, buttons) is drawn into one group and faded as one picture, so its
        // text never lags its paper (each child blended on its own left dark ink invisible on the dark cover).
        _askGroup = new CanvasGroup { Name = "QuestionGroup" };
        _ask.AddChild(_askGroup);
        _yes = Item("Start a new town", ConfirmNewTown, _ask);
        _no = Item("Keep this town", CancelAsk, _ask);
        _coverItems = new[] { _resume, _settings, _newTown, _quit };
        _askItems = new[] { _yes, _no };
        _allItems = new[] { _resume, _settings, _newTown, _quit, _yes, _no };
        Chain(_resume, _settings, _newTown, _quit);
        Chain(_yes, _no);
        // Round 7: the settings spread is built once, here at start-up, and kept: opening it, or turning to another of
        // its sections, never builds anything (it is built again only when the window or the interface size changes).
        _pages = new SettingsPages(_main.Settings, _main.Saver) { Visible = false, Live = false, OwnTable = false };
        _pages.Closed += CloseSettings;
        _pages.PageTurned += () => _main.Audio.PageTurn();
        AddChild(_pages);
        _name.Text = TownName.Of(_main.Driver.Seed);
        WarmUp(true);
    }

    /// <summary>
    /// Start-up warm-up, two frames: the lettering, the question and all four settings sections are laid out and drawn
    /// once at no opacity, so their fonts are rasterised at the sizes used and their controls laid out before the
    /// first transition shows them (otherwise that work would land in its first frame).
    /// </summary>
    private void WarmUp(bool on)
    {
        var clear = new Color(1, 1, 1, 0);
        _cover.Modulate = _ask.Modulate = _pages.Modulate = clear;
        _cover.Visible = _ask.Visible = on;
        _pages.Visible = on;
        _pages.MouseBehaviorRecursive = on ? Control.MouseBehaviorRecursiveEnum.Disabled : Control.MouseBehaviorRecursiveEnum.Inherited;
        _pages.FocusBehaviorRecursive = on ? Control.FocusBehaviorRecursiveEnum.Disabled : Control.FocusBehaviorRecursiveEnum.Inherited;
        foreach (var b in _allItems) b.MouseFilter = on ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        _cover.MouseFilter = _ask.MouseFilter = on ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        if (_pages.IsInsideTree()) _pages.WarmUp(on);
        if (on) Layout(true);
        else
        {
            // Building the spread made thousands of short-lived wrappers (theme names and the like) that Godot's tracker of
            // C# objects holds until they are collected. Collected now, at start-up, the tracker keeps the room they took,
            // so it need not grow (a large allocation in one frame) later in play.
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private Label Letter(string text, FontFile font, Color c)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontOverride("font", font);
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeColorOverride("font_shadow_color", new Color(0.15f, 0.06f, 0.03f, 0.7f));
        _cover.AddChild(l);
        return l;
    }

    /// <summary>A menu line in gilt: no button face; hover and focus brighten it and tool a gilt frame round it.</summary>
    private Button Item(string text, Action act, Control? parent = null)
    {
        var b = new Button { Name = text.Replace(" ", ""), Text = text, FocusMode = Control.FocusModeEnum.All, Flat = false };
        b.AddThemeFontOverride("font", UiKit.Serif);
        b.AddThemeColorOverride("font_color", Gilt);
        b.AddThemeColorOverride("font_hover_color", GiltBright);
        b.AddThemeColorOverride("font_focus_color", GiltBright);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_shadow_color", new Color(0.15f, 0.06f, 0.03f, 0.7f));
        b.Pressed += () => { _main.Audio.UiPress(); act(); };
        b.MouseEntered += () => { if (b.IsVisibleInTree() && _warm <= 0) b.GrabFocus(); }; // focus follows the mouse (and plays ui-hover)
        b.FocusEntered += () => { if (_warm <= 0) _main.Audio.UiHover(); };
        (parent ?? _cover).AddChild(b);
        return b;
    }

    private static void Chain(params Button[] bs)
    {
        for (int i = 0; i < bs.Length; i++)
        {
            var up = bs[(i + bs.Length - 1) % bs.Length];
            var down = bs[(i + 1) % bs.Length];
            bs[i].FocusNeighborTop = bs[i].GetPathTo(up);
            bs[i].FocusNeighborBottom = bs[i].GetPathTo(down);
            bs[i].FocusNeighborLeft = bs[i].GetPathTo(up);
            bs[i].FocusNeighborRight = bs[i].GetPathTo(down);
        }
    }

    private void Restyle(float s)
    {
        _s = s;
        var empty = new StyleBoxEmpty();
        var frame = new StyleBoxFlat
        {
            BgColor = new Color(Gilt, 0.10f), BorderColor = GiltBright, DrawCenter = true,
            ContentMarginLeft = UiKit.Px(s, 26), ContentMarginRight = UiKit.Px(s, 26),
            ContentMarginTop = UiKit.Px(s, 4), ContentMarginBottom = UiKit.Px(s, 6),
        };
        frame.SetBorderWidthAll(Math.Max(2, UiKit.Px(s, 2)));
        frame.SetCornerRadiusAll(UiKit.Px(s, 3));
        var plain = (StyleBoxFlat)frame.Duplicate();
        plain.DrawCenter = false; plain.BorderColor = new Color(0, 0, 0, 0);
        // Round 4: on the New town question both choices wear a dim gilt frame before the pointer is on them.
        var choice = (StyleBoxFlat)frame.Duplicate();
        choice.BgColor = new Color(Gilt, 0.04f); choice.BorderColor = new Color(GiltDim, 0.85f);
        foreach (var b in _allItems)
        {
            if (b.GetParent() != _cover) continue;
            b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, b.GetParent() == _ask ? 34 : 44));
            b.AddThemeStyleboxOverride("normal", b.GetParent() == _ask ? choice : plain);
            b.AddThemeStyleboxOverride("hover", frame);
            b.AddThemeStyleboxOverride("pressed", frame);
            b.AddThemeStyleboxOverride("focus", frame);
            b.AddThemeStyleboxOverride("hover_pressed", frame);
            b.AddThemeStyleboxOverride("disabled", empty);
            b.AddThemeConstantOverride("shadow_offset_x", UiKit.Px(s, 1));
            b.AddThemeConstantOverride("shadow_offset_y", UiKit.Px(s, 2));
        }
        _title.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, 96));
        _name.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, 40));
        foreach (var l in new[] { _title, _name })
        {
            l.AddThemeConstantOverride("shadow_offset_x", UiKit.Px(s, 1));
            l.AddThemeConstantOverride("shadow_offset_y", UiKit.Px(s, 2));
        }
        BuildAskCard(s);
        _laidRect = default;
    }

    // ------------------------------------------------------------------ input

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        bool pause = e.IsActionPressed("tt_pause") || k.Keycode == Key.Escape;
        bool cancel = pause || e.IsActionPressed("ui_cancel");
        switch (Now)
        {
            case State.Playing when pause && !_main.Hud.TakesEsc: Pause(); break;
            // Impatient input: Esc while the book closes opens it again from where it is; while it opens, closes it.
            // (A turn-round asked for in a transition's first TurnGap waits for it: see PauseMenu.Tail.cs.)
            case State.Closing when cancel: if (!HoldTurn()) Resume(); break;
            case State.Opening when pause: if (!HoldTurn()) Pause(); break;
            case State.Cover when cancel: Resume(); break;
            case State.Asking when cancel: CancelAsk(); break;
            // On the way back from the settings Esc means what it means on the cover: back to the town.
            case State.Returning when cancel: Resume(); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (_warm > 0 && --_warm == 0) WarmUp(false);
        SaveDue();
        if (_motion.Running) Step(delta);
        else if (Now == State.Settings) HoldSpreadView();
        TurnHeld();
        if (_cover.Visible || _ask.Visible) Layout(false);
    }

    // ------------------------------------------------------------------ the cover's lettering

    /// <summary>The closed cover's rectangle on screen (canvas units).</summary>
    public Rect2 CoverRect()
    {
        var cam = _main.Rig.Camera;
        var book = _main.Book.GlobalTransform;
        var c = BookCover.ClosedCentre; var h = BookCover.ClosedSize / 2f;
        var a = cam.UnprojectPosition(book * new Vector3(c.X - h.X, BookCover.ClosedTop, c.Z - h.Y));
        var b = cam.UnprojectPosition(book * new Vector3(c.X + h.X, BookCover.ClosedTop, c.Z + h.Y));
        // UnprojectPosition works in the viewport's visible rect, the canvas units the interface is laid out in.
        return new Rect2(new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), (a - b).Abs());
    }

    private void Layout(bool force)
    {
        float s = UiKit.ScaleFor(GetWindow().Size.Y);
        if (MathF.Abs(s - _s) > 1e-4f) Restyle(s);
        var r = CoverRect();
        float slide = _askSlide;
        if (!force && r == _laidRect && slide == _laidSlide) return;
        bool moved = r != _laidRect;
        _laidRect = r; _laidSlide = slide;
        _cover.Position = r.Position; _cover.Size = r.Size;
        var view = GetViewport().GetVisibleRect().Size;
        _ask.Position = new Vector2(0, slide * UiKit.Px(s, 28)); _ask.Size = view;
        _askScrim.Position = new Vector2(0, -slide * UiKit.Px(s, 28)); _askScrim.Size = view;
        if (_askCard != null) ModalCard.Centre(_askCard, view);
        if (!moved && !force) return;
        _cover.QueueRedraw();
        float w = r.Size.X;
        Place(_title, w, r.Size.Y * 0.16f);
        Place(_name, w, _title.Position.Y + _title.Size.Y + UiKit.Px(s, 4));
        float y = r.Size.Y * 0.47f;
        foreach (var b in _coverItems)
        {
            b.ResetSize();
            b.Position = new Vector2((w - b.Size.X) / 2f, y);
            y += b.Size.Y + UiKit.Px(s, 14);
        }
    }

    /// <summary>The question's modal card, built again for an interface size (the two buttons move into it).</summary>
    private void BuildAskCard(float s)
    {
        foreach (var b in _askItems) b.GetParent()?.RemoveChild(b);
        if (_askCard != null) { _askGroup.RemoveChild(_askCard); _askCard.QueueFree(); }
        _askCard = ModalCard.Build(s, QuestionTitle, QuestionBody, out var body, out var row, "QuestionCard");
        _askText = body;
        _askGroup.AddChild(_askCard);
        row.AddChild(ModalCard.Button(_yes, s, primary: false));
        row.AddChild(ModalCard.Button(_no, s, primary: true));
        Chain(_yes, _no);
    }

    private static void Place(Control c, float w, float y)
    {
        c.ResetSize();
        c.Position = new Vector2((w - c.Size.X) / 2f, y);
    }

    /// <summary>Tooled lines in gilt: a double frame inside the board's rules, a rule under the name, corner leaves.</summary>
    private void DrawTooling(Control page)
    {
        // The question's lines slide in with it, but its tooled frame stays put on the board (it lies exactly over the
        // cover's own, so the two cross-fade as one).
        var sz = page.Size;
        float s = Math.Max(0.5f, _s);
        float i1 = sz.X * 0.085f, i2 = i1 + UiKit.Px(s, 8);
        float lw = Math.Max(1.5f, 2f * s);
        page.DrawRect(new Rect2(i1, i1, sz.X - 2 * i1, sz.Y - 2 * i1), GiltDim, false, lw);
        page.DrawRect(new Rect2(i2, i2, sz.X - 2 * i2, sz.Y - 2 * i2), Gilt, false, Math.Max(1f, lw * 0.6f));
        // Corner leaves: a small gilt diamond in each corner between the frames.
        float d = UiKit.Px(s, 10);
        Span<Vector2> diamond = stackalloc Vector2[4];
        for (int k = 0; k < 4; k++)
        {
            var c = new Vector2(k % 2 == 0 ? i2 : sz.X - i2, k < 2 ? i2 : sz.Y - i2);
            var cx = c.X + (c.X < sz.X / 2 ? d * 1.6f : -d * 1.6f);
            var cy = c.Y + (c.Y < sz.Y / 2 ? d * 1.6f : -d * 1.6f);
            diamond[0] = new Vector2(cx, cy - d); diamond[1] = new Vector2(cx + d, cy);
            diamond[2] = new Vector2(cx, cy + d); diamond[3] = new Vector2(cx - d, cy);
            page.DrawColoredPolygon(diamond, Gilt, default, null);
        }
        if (page == _cover)
        {
            float y = _name.Position.Y + _name.Size.Y + UiKit.Px(s, 18);
            float half = sz.X * 0.22f;
            page.DrawLine(new Vector2(sz.X / 2 - half, y), new Vector2(sz.X / 2 - d, y), Gilt, lw);
            page.DrawLine(new Vector2(sz.X / 2 + d, y), new Vector2(sz.X / 2 + half, y), Gilt, lw);
            float e = d * 0.6f;
            diamond[0] = new Vector2(sz.X / 2, y - e); diamond[1] = new Vector2(sz.X / 2 + e, y);
            diamond[2] = new Vector2(sz.X / 2, y + e); diamond[3] = new Vector2(sz.X / 2 - e, y);
            page.DrawColoredPolygon(diamond, Gilt, default, null);
        }
    }

    // ------------------------------------------------------------------ the settings spread's view

    /// <summary>
    /// The camera for the open book under the settings spread: straight down, the 3D pages exactly under the 2D pages
    /// (their height and centre), so the spread arrives on the paper as a cross-fade.
    /// </summary>
    /// <summary>The settings pages' rectangle on screen (Main: the zoom's outer end is the spread view).</summary>
    public Rect2 SpreadPagesRect => _pages.PagesRect;

    public (Vector3 centre, float size) SpreadView()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        var pages = _pages.PagesRect;
        if (pages.Size.Y < 1f || vp.Y < 1f) return _main.ClosedBookView();
        float size = BookBuilder.PageDepth * vp.Y / pages.Size.Y;
        float m = size / vp.Y;
        var d = pages.GetCenter() - vp / 2f;
        var world = _main.Book.GlobalTransform * new Vector3(-d.X * m, 0, -d.Y * m);
        var local = _main.MapOrigin.GlobalTransform.AffineInverse() * world;
        return (new Vector3(local.X, 0, local.Z), size);
    }

    private void HoldSpreadView()
    {
        var (c, size) = SpreadView();
        var rig = _main.Rig;
        if (rig.PivotLocal.DistanceTo(c) > 1e-3f || MathF.Abs(rig.Camera.Size - size) > 1e-3f) rig.Place(c, size);
    }

    // ------------------------------------------------------------------ tests and frame strips

    /// <summary>Holds the transition under way (and the next one started) at this share of its length; negative lets it run.</summary>
    public void HoldAt(float share)
    {
        if (share < 0f) { _motion.Release(); return; }
        _motion.Hold(share * _motion.Length);
    }
}
