using System;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game;

/// <summary>
/// Pausing closes the book and the closed cover is the menu. tt_pause (Esc) or the interface's pause mark: the camera
/// pulls back to the book, the front board swings shut (BookCover, book-close sound), the simulation stops, the pause
/// piece plays and the town is saved. On the cover, in gilt: the title, the town's name, and Resume, Settings, New town,
/// Quit (mouse, or up, down and Enter; Esc resumes). Resume opens the book and puts the camera back where it was.
/// Settings opens the book at the settings pages (SettingsPages); Back returns to the cover. New town asks once.
/// Quit saves and quits.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public static readonly Color Gilt = Color.FromHtml("#D9B45A");
    public static readonly Color GiltBright = Color.FromHtml("#F4D98C");
    public static readonly Color GiltDim = Color.FromHtml("#A8873F");
    public const string Question = "Start a new town? This town will be lost.";

    public enum State { Playing, Closing, Cover, Settings, Asking, Opening }
    public State Now { get; private set; } = State.Playing;
    public bool IsPaused => Now != State.Playing;
    public SettingsPages? Pages { get; private set; }

    private Main _main = null!;
    private Control _root = null!, _cover = null!, _ask = null!;
    private Label _title = null!, _name = null!, _askText = null!;
    private Button _resume = null!, _settings = null!, _newTown = null!, _quit = null!, _yes = null!, _no = null!;
    private Vector3 _keptPivot;
    private float _keptSize;
    private bool _keptMoved, _followAfter;
    private float _s = -1;

    public Button ResumeButton => _resume;
    public Button SettingsButton => _settings;
    public Button NewTownButton => _newTown;
    public Button QuitButton => _quit;
    public Button YesButton => _yes;
    public Button NoButton => _no;
    public bool CoverShown => _cover.Visible;
    public bool QuestionShown => _ask.Visible;
    public string QuestionText => _askText.Text;

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
        _cover.Draw += DrawTooling;
        _title = Letter("Tide & Toll", UiKit.Display, Gilt);
        _name = Letter("", UiKit.Serif, GiltDim);
        _resume = Item("Resume", Resume);
        _settings = Item("Settings", OpenSettings);
        _newTown = Item("New town", Ask);
        _quit = Item("Quit", SaveAndQuit);
        _ask = new Control { Name = "Question", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddChild(_ask);
        _ask.Draw += DrawTooling;
        _askText = new Label { Text = Question, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _ask.AddChild(_askText);
        _yes = Item("Start a new town", ConfirmNewTown, _ask);
        _no = Item("Keep this town", CancelAsk, _ask);
        Chain(_resume, _settings, _newTown, _quit);
        Chain(_yes, _no);
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
        b.MouseEntered += () => { if (b.IsVisibleInTree()) b.GrabFocus(); }; // focus follows the mouse (and plays ui-hover)
        b.FocusEntered += () => _main.Audio.UiHover();
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
        foreach (var b in new[] { _resume, _settings, _newTown, _quit, _yes, _no })
        {
            b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, b.GetParent() == _ask ? 34 : 44));
            b.AddThemeStyleboxOverride("normal", plain);
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
        _askText.AddThemeFontOverride("font", UiKit.Serif);
        _askText.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, 40));
        _askText.AddThemeColorOverride("font_color", GiltBright);
    }

    // ------------------------------------------------------------------ the flow

    public void Pause()
    {
        if (Now != State.Playing) return;
        if (_main.Hud.PaveMode) _main.Hud.SetPaveMode(false);
        _main.Hud.CloseAchievements(); _main.Hud.CloseAgePage();
        var rig = _main.Rig;
        _keptPivot = rig.PivotLocal; _keptSize = rig.Camera.Size; _keptMoved = rig.UserMoved;
        _followAfter = false;
        _main.Driver.SetPaused(true);
        _main.Hud.Visible = false;
        rig.InputLocked = true;
        rig.UserMoved = true;
        var (centre, size) = _main.ClosedBookView();
        rig.LookAtFree(centre, size, instant: false);
        _main.Cover.Close();
        _main.Audio.BookClose();
        _main.Audio.SetPauseMusic(true);
        _main.Saver.SaveNow();
        Now = State.Closing;
    }

    public void Resume()
    {
        if (Now is State.Playing or State.Opening) return;
        HideAll();
        _main.Cover.Open();
        _main.Audio.BookOpen();
        _main.Audio.SetPauseMusic(false);
        if (_followAfter) { _main.Rig.UserMoved = false; _main.Follow(instant: false); }
        else { _main.Rig.LookAt(_keptPivot, _keptSize, instant: false); _main.Rig.UserMoved = _keptMoved; }
        Now = State.Opening;
    }

    /// <summary>While the book is closed, a load or a new town sets where Resume goes (moved false: follow the town).</summary>
    public void KeepView(Vector3 pivot, float size, bool moved)
    {
        _keptPivot = pivot; _keptSize = size; _keptMoved = moved;
        _followAfter = !moved;
    }

    /// <summary>Where the camera was when the book closed (what Resume returns to): pivot, view height, moved by hand.</summary>
    public (Vector3 pivot, float size, bool moved) Kept => (_keptPivot, _keptSize, _keptMoved);

    private void ShowCover()
    {
        HideAll();
        _name.Text = TownName.Of(_main.Driver.Seed);
        _cover.Visible = true;
        Now = State.Cover;
        _resume.GrabFocus();
    }

    private void HideAll()
    {
        _cover.Visible = false;
        _ask.Visible = false;
        if (Pages != null) { Pages.QueueFree(); Pages = null; }
    }

    public void OpenSettings()
    {
        if (Now != State.Cover) return;
        _cover.Visible = false;
        _main.Audio.BookOpen();
        Pages = new SettingsPages(_main.Settings, _main.Saver);
        Pages.Closed += CloseSettings;
        AddChild(Pages);
        Now = State.Settings;
    }

    private void CloseSettings()
    {
        _main.Audio.BookClose();
        var p = Pages;
        Pages = null;
        p?.QueueFree();
        ShowCover();
        _settings.GrabFocus();
    }

    private void Ask()
    {
        if (Now != State.Cover) return;
        _cover.Visible = false;
        _ask.Visible = true;
        Now = State.Asking;
        _no.GrabFocus();
    }

    private void CancelAsk()
    {
        ShowCover();
        _newTown.GrabFocus();
    }

    private void ConfirmNewTown()
    {
        _main.NewTown();
        _followAfter = true;
        Now = State.Cover;
        Resume();
    }

    private void SaveAndQuit() => _main.QuitGame();

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        bool pause = e.IsActionPressed("tt_pause") || k.Keycode == Key.Escape;
        bool cancel = pause || e.IsActionPressed("ui_cancel");
        switch (Now)
        {
            case State.Playing when pause && !_main.Hud.TakesEsc: Pause(); break;
            case State.Cover when cancel: Resume(); break;
            case State.Asking when cancel: CancelAsk(); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (Now == State.Closing && _main.Cover.Closed && MathF.Abs(_main.Rig.Camera.Size - _main.Rig.TargetSize) < 0.5f) ShowCover();
        if (Now == State.Opening && !_main.Cover.Moving)
        {
            Now = State.Playing;
            _main.Driver.SetPaused(false);
            _main.Hud.Visible = true;
            _main.Rig.InputLocked = false;
        }
        if (_cover.Visible || _ask.Visible) Layout();
    }

    /// <summary>The closed cover's rectangle on screen (canvas units).</summary>
    public Rect2 CoverRect()
    {
        var cam = _main.Rig.Camera;
        var book = _main.Book.GlobalTransform;
        var c = BookCover.ClosedCentre; var h = BookCover.ClosedSize / 2f;
        var a = cam.UnprojectPosition(book * new Vector3(c.X - h.X, 6f, c.Z - h.Y));
        var b = cam.UnprojectPosition(book * new Vector3(c.X + h.X, 6f, c.Z + h.Y));
        // UnprojectPosition works in the viewport's visible rect, the canvas units the interface is laid out in.
        return new Rect2(new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), (a - b).Abs());
    }

    private void Layout()
    {
        float s = UiKit.ScaleFor(GetWindow().Size.Y);
        if (MathF.Abs(s - _s) > 1e-4f) Restyle(s);
        var r = CoverRect();
        foreach (var page in new[] { _cover, _ask }) { page.Position = r.Position; page.Size = r.Size; page.QueueRedraw(); }
        float w = r.Size.X;
        Place(_title, w, r.Size.Y * 0.16f);
        Place(_name, w, _title.Position.Y + _title.Size.Y + UiKit.Px(s, 4));
        float y = r.Size.Y * 0.47f;
        foreach (var b in new[] { _resume, _settings, _newTown, _quit })
        {
            b.ResetSize();
            b.Position = new Vector2((w - b.Size.X) / 2f, y);
            y += b.Size.Y + UiKit.Px(s, 14);
        }
        _askText.Size = new Vector2(w * 0.78f, 0);
        _askText.ResetSize();
        _askText.Size = new Vector2(w * 0.78f, _askText.Size.Y);
        _askText.Position = new Vector2(w * 0.11f, r.Size.Y * 0.30f);
        y = _askText.Position.Y + _askText.Size.Y + UiKit.Px(s, 40);
        foreach (var b in new[] { _yes, _no })
        {
            b.ResetSize();
            b.Position = new Vector2((w - b.Size.X) / 2f, y);
            y += b.Size.Y + UiKit.Px(s, 14);
        }
    }

    private static void Place(Control c, float w, float y)
    {
        c.ResetSize();
        c.Position = new Vector2((w - c.Size.X) / 2f, y);
    }

    /// <summary>Tooled lines in gilt: a double frame inside the board's rules, a rule under the name, corner leaves.</summary>
    private void DrawTooling()
    {
        var page = _cover.Visible ? _cover : _ask;
        var sz = page.Size;
        float s = Math.Max(0.5f, _s);
        float i1 = sz.X * 0.085f, i2 = i1 + UiKit.Px(s, 8);
        float lw = Math.Max(1.5f, 2f * s);
        page.DrawRect(new Rect2(i1, i1, sz.X - 2 * i1, sz.Y - 2 * i1), GiltDim, false, lw);
        page.DrawRect(new Rect2(i2, i2, sz.X - 2 * i2, sz.Y - 2 * i2), Gilt, false, Math.Max(1f, lw * 0.6f));
        // Corner leaves: a small gilt diamond in each corner between the frames.
        float d = UiKit.Px(s, 10);
        foreach (var c in new[] { new Vector2(i2, i2), new Vector2(sz.X - i2, i2), new Vector2(i2, sz.Y - i2), new Vector2(sz.X - i2, sz.Y - i2) })
        {
            var cx = c.X + (c.X < sz.X / 2 ? d * 1.6f : -d * 1.6f);
            var cy = c.Y + (c.Y < sz.Y / 2 ? d * 1.6f : -d * 1.6f);
            page.DrawColoredPolygon(new[] { new Vector2(cx, cy - d), new Vector2(cx + d, cy), new Vector2(cx, cy + d), new Vector2(cx - d, cy) }, Gilt);
        }
        if (page == _cover)
        {
            float y = _name.Position.Y + _name.Size.Y + UiKit.Px(s, 18);
            float half = sz.X * 0.22f;
            page.DrawLine(new Vector2(sz.X / 2 - half, y), new Vector2(sz.X / 2 - d, y), Gilt, lw);
            page.DrawLine(new Vector2(sz.X / 2 + d, y), new Vector2(sz.X / 2 + half, y), Gilt, lw);
            page.DrawColoredPolygon(new[] { new Vector2(sz.X / 2, y - d * 0.6f), new Vector2(sz.X / 2 + d * 0.6f, y), new Vector2(sz.X / 2, y + d * 0.6f), new Vector2(sz.X / 2 - d * 0.6f, y) }, Gilt);
        }
    }
}
