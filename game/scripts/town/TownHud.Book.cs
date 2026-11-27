using System;
using System.Text;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 3: the achievements page while zoomed in (tt_achievements or its bookmark; the left page of the book carries
/// the same list when the book is in view, TownSummary), and the end of the age: the charter's banner, "Ring in the
/// next age" as a moment (the bell peals, the rising theme, a banner) and then the honest page that says the first
/// age is complete, what the town achieved, and that the river comes next.
/// </summary>
public partial class TownHud
{
    public const float AgePageDelay = 3.2f;
    private PanelContainer _achPanel = null!, _agePanel = null!;
    private Label _achHead = null!, _ageText = null!, _ageTitle = null!;
    private readonly Label[] _achName = new Label[32], _achState = new Label[32];
    private readonly ColorRect[] _achFill = new ColorRect[32];
    private readonly Control[] _achBack = new Control[32];
    // Round 5: the page shows the age the town is in (the river's 16 in the second age, with the first age's count).
    private readonly Label[] _achDesc = new Label[32];
    private readonly Control[] _achCell = new Control[32];
    // Night 3: each achievement's medal (assets/icons/ach-*.svg) at the left of its cell, dimmed until done.
    private readonly TextureRect[] _achIcon = new TextureRect[32];
    private int _achBuiltCount;
    private bool _achRiver;
    private static AchievementView Ach(Town t, bool river, int i) => river ? t.RiverAchievement(i) : t.Achievement(i);
    private static int AchCount(Town t, bool river) => river ? t.RiverAchievementCount : t.AchievementCount;

    /// <summary>Puts the age's achievements into the page's cells (names, descriptions); cells beyond its count hide.</summary>
    private void AchAge(Town t)
    {
        _achRiver = t.InRiverAge;
        int n = AchCount(t, _achRiver);
        for (int i = 0; i < _achBuiltCount; i++)
        {
            bool on = i < n;
            _achCell[i].Visible = on;
            if (!on) continue;
            var a = Ach(t, _achRiver, i);
            _achName[i].Text = a.Name;
            _achDesc[i].Text = a.Description;
            _achIcon[i].Texture = AchIcon(a, _achRiver);
        }
        _achShown = -1;
    }
    /// <summary>
    /// The medal of an achievement: first age "ach-" + its Id with "_" as "-"; the river "ach-" + its name in lower case,
    /// apostrophes dropped, spaces as "-". The plain achievement mark stands in for a missing file.
    /// </summary>
    public static Texture2D? AchIcon(in AchievementView a, bool river)
    {
        string key = river
            ? a.Name.ToLowerInvariant().Replace("'", "").Replace("\u2019", "").Replace(' ', '-')
            : (a.Id ?? "").Replace('_', '-');
        return IconOr("ach-" + key, "ui-achievement");
    }
    private Button _achMark = null!, _achClose = null!, _ageClose = null!;
    private int _achShown = -1, _achSig = -1;
    private Town? _achCountTown;
    private long _achCountTick = -1;
    private bool _achCountRiver;
    private double _achClock;
    private float _agePageIn = -1;

    public bool AchievementsOpen => _achFade.On;
    public bool AgePageOpen => _ageFade.On;
    /// <summary>Esc closes something of the interface first instead of pausing.</summary>
    public bool TakesEsc => PaveMode || ExtendMode || AchievementsOpen || AgePageOpen;
    public Button AchievementsMark => _achMark;
    public Button AgePageClose => _ageClose;
    public string AgePageText => _ageTitle.Text + "\n" + _ageText.Text;
    /// <summary>The rows as printed: "name: state" per achievement (tests).</summary>
    public string AchievementRows()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _drv.Town.AchievementCount && i < _achName.Length; i++) sb.Append(_achName[i].Text).Append(": ").Append(_achState[i].Text).Append('\n');
        return sb.ToString();
    }

    /// <summary>"Done", "37 of 100", or a share for a target of one ("45 %").</summary>
    public static string AchievementState(in AchievementView a)
    {
        if (a.Unlocked) return "Done";
        if (a.Target <= 1) return a.Value > 0 && a.Value < 1 ? $"{Math.Floor(a.Value * 100)} %" : "Not yet";
        return $"{Money(Math.Min(a.Value, a.Target))} of {Money(a.Target)}";
    }

    private void BuildBookParts()
    {
        _achPanel = Page("AchievementsPage", 1120);
        var col = (VBoxContainer)_achPanel.GetChild(0);
        _achHead = PlayText("Achievements", UiKit.Title, UiKit.Serif);
        _achHead.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_achHead);
        var rule = UiKit.MutedRule(); rule.CustomMinimumSize = new Vector2(0, 1); col.AddChild(rule);
        // Three columns when the page is wide enough (24 achievements in 8 rows fit 600 high), else two.
        float inner = Zones.Page.Size.X - UiKit.U(_s, 48);
        int cols = inner >= UiKit.U(_s, 840) ? 3 : 2;
        float cellW = (inner - (cols - 1) * UiKit.U(_s, 24)) / cols;
        var grid = new GridContainer { Columns = cols, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", UiKit.U(_s, 24));
        grid.AddThemeConstantOverride("v_separation", UiKit.U(_s, 6));
        col.AddChild(grid);
        int n = Math.Min(_drv.Town.AchievementCount, _achName.Length);
        int gap = UiKit.U(_s, 8);
        float textW = cellW - UiKit.U(_s, 24) - gap;
        for (int i = 0; i < n; i++)
        {
            var a = _drv.Town.Achievement(i);
            var outer = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(cellW, 0) };
            outer.AddThemeConstantOverride("separation", gap);
            _achIcon[i] = UiKit.IconRect(_s, null, 24);
            _achIcon[i].SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            outer.AddChild(_achIcon[i]);
            var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(textW, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            outer.AddChild(cell);
            cell.AddThemeConstantOverride("separation", 0);
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            _achName[i] = PlayText(a.Name, UiKit.Body, UiKit.UiBold);
            _achName[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _achName[i].ClipText = true; _achName[i].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _achName[i].CustomMinimumSize = new Vector2(textW * 0.6f, 0);
            _achState[i] = PlayText("", UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
            row.AddChild(_achName[i]);
            row.AddChild(_achState[i]);
            cell.AddChild(row);
            var desc = PlayText(a.Description, UiKit.Caption, UiKit.UiFont, UiKit.Muted);
            desc.ClipText = true; desc.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            desc.CustomMinimumSize = new Vector2(textW, 0);
            cell.AddChild(desc);
            _achDesc[i] = desc;
            _achCell[i] = outer;
            var back = UiKit.Bar(_s, UiKit.RiverBlue, out _achFill[i]);
            back.CustomMinimumSize = new Vector2(0, UiKit.U(_s, 8));
            _achBack[i] = back;
            cell.AddChild(back);
            grid.AddChild(outer);
        }
        _achBuiltCount = n;
        AchAge(_drv.Town);
        _achClose = new Button { Name = "Close", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        UiKit.StyleButton(_achClose, _s, 6);
        _achClose.CustomMinimumSize = new Vector2(UiKit.U(_s, 160), UiKit.U(_s, 32));
        _achClose.Pressed += () => CloseAchievements();
        col.AddChild(_achClose);

        _agePanel = Page("FirstAgePage", 820);
        var ac = (VBoxContainer)_agePanel.GetChild(0);
        _ageTitle = PlayText("The first age is complete", UiKit.DisplaySize, UiKit.Display);
        _ageTitle.HorizontalAlignment = HorizontalAlignment.Center;
        ac.AddChild(_ageTitle);
        var arule = UiKit.MutedRule(); arule.CustomMinimumSize = new Vector2(0, 1); ac.AddChild(arule);
        _ageText = PlayText("", UiKit.Body, UiKit.Serif);
        _ageText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _ageText.CustomMinimumSize = new Vector2(Zones.Page.Size.X - UiKit.U(_s, 48), 0);
        ac.AddChild(_ageText);
        _ageClose = new Button { Name = "KeepPlaying", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        UiKit.StyleButton(_ageClose, _s, 6);
        _ageClose.CustomMinimumSize = new Vector2(UiKit.U(_s, 160), UiKit.U(_s, 32));
        _ageClose.Pressed += () => CloseAgePage();
        ac.AddChild(_ageClose);
        _achShown = -1; _achSig = -1;
        NewPageFades();
    }

    /// <summary>A label in one of the sheet's sizes (no floor: one scale for boxes and type).</summary>
    private Label PlayText(string text, float size, FontFile font, Color? colour = null)
    {
        var l = UiKit.Text(text, _s, size, font, colour);
        l.AddThemeFontSizeOverride("font_size", UiKit.Type(_s, size));
        return l;
    }

    /// <summary>A page of the book over the town: paper with a double ink edge, centred.</summary>
    private PanelContainer Page(string name, float width)
    {
        var p = new PanelContainer { Name = name, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        // Night 3 (layout sheet v2, the Page zone): card style, min(960, W-64) wide, centred (HudZones.Page).
        var box = UiKit.CardBox(_s, UiKit.CardKind.Card, 24);
        box.BgColor = UiKit.Paper; // pages are opaque (lead's note 21:05)
        p.AddThemeStyleboxOverride("panel", box);
        p.CustomMinimumSize = new Vector2(Zones.Page.Size.X, 0);
        _ = width;
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.U(_s, 8));
        p.AddChild(col);
        _root.AddChild(p);
        return p;
    }

    public void ToggleAchievements() { if (AchievementsOpen) CloseAchievements(); else OpenAchievements(); }

    public void OpenAchievements()
    {
        if (_fullA < 0.5f) return; // the book is in view: the left page shows them
        CloseAgePage();
        SetPaveMode(false); SetExtendMode(false);
        ShowPage(_achFade);
        _achClock = 1e9;
        _main.Audio.UiTab();
    }

    /// <summary>Closes the achievements page; <paramref name="quiet"/>: without its sound (the pause menu, once the interface has faded out).</summary>
    public void CloseAchievements(bool quiet = false)
    {
        if (_achPanel == null || !_achFade.On) { if (quiet) HidePage(_achFade, true); return; }
        HidePage(_achFade, quiet);
        if (!quiet) _main.Audio.UiTab();
    }

    public void CloseAgePage(bool quiet = false)
    {
        if (_agePanel == null || !_ageFade.On) { if (quiet) HidePage(_ageFade, true); return; }
        HidePage(_ageFade, quiet);
        if (!quiet) _main.Audio.PageTurn();
    }

    /// <summary>The Ring bookmark or its key: rings the next age in the first time, opens the age page afterwards.</summary>
    public void NextAgePressed()
    {
        var t = _drv.Town;
        if (t.CanRingNextAge && !t.NextAgeRung)
        {
            if (!t.RingNextAge()) return;
            _main.Audio.NextAgeRung();
            _main.Buildings.Peal(6f);
            Large(AgeBannerHead, "The town turns to the river: a landing, boats and crossings.", PrioMoment, BannerStay, "");
            FocusBellGreen();
            RiverRung();
            _agePageIn = AgePageDelay;
        }
        else if (t.NextAgeRung) OpenAgePage();
    }

    /// <summary>The view glides to the bell green (the charter stone and the bell), a little below the banner.</summary>
    public void FocusBellGreen()
    {
        foreach (var p in _drv.Town.Plan.Plots)
        {
            if (p.Kind != PlotKind.BellGreen) continue;
            var c = new Vector3((p.X + p.W / 2f) * TownGround.Cell, 0, (p.Y + p.H / 2f - 1.5f) * TownGround.Cell);
            _main.Rig.UserMoved = true;
            _main.Rig.LookAt(c, Math.Max(_main.Rig.MinSize, Math.Min(_main.Rig.Camera.Size * 0.7f, _main.Rig.MapViewSize * 0.6f)), instant: false);
            return;
        }
    }

    public void OpenAgePage()
    {
        CloseAchievements();
        var t = _drv.Town;
        if (t.InRiverAge) { OpenRiverPage(); return; }
        int paved = t.CountPaved(), roads = TownSummary.OpenRoads(t);
        long mins = (long)(t.Time / 60.0);
        string time = mins >= 60 ? $"{mins / 60} h {mins % 60} min" : $"{mins} min";
        bool mill = false;
        foreach (var pl in t.Plan.Plots) if (pl.Kind == PlotKind.Mill && t.PlotWorking(pl.Id)) mill = true;
        _ageText.Text =
            $"{TownName.Of(t.Seed)} has its charter. In {time} it grew from one cottage and a well to {TownSummary.Cottages(t)} cottages " +
            $"and {t.VillagerCount} villagers{(mill ? ", with a market and a mill" : "")}.\n\n" +
            $"Roads: {roads}, of them {paved} paved.\nCoin earned: {Money(t.CoinEarned)}.\nTrips: {Money(t.Trips)}.\n" +
            $"Requests met: {t.RequestsMet} of {t.RequestsAsked}.\nAchievements: {TownSummary.Achieved(t)} of {t.AchievementCount}.\n\n" +
            "The river comes next: boats, a ford and a ferry. That age is not in the game yet. " +
            "Your town stays as it is, and you can keep playing it.";
        _ageClose.Text = "Keep playing" + Settings.GameInput.Tag("tt_pause");
        ShowPage(_ageFade);
        _main.Audio.PageTurn();
    }

    private void BookFrame(float dt)
    {
        var t = _drv.Town;
        if (_agePageIn > 0 && HoldPageTurnAt < 0) { _agePageIn -= dt; if (_agePageIn <= 0) OpenAgePage(); }
        StepPage(_achFade, dt);
        StepPage(_ageFade, dt);
        float pa = Math.Max(_achPanel.Visible ? _achFade.A : 0f, _agePanel.Visible ? _ageFade.A : 0f);
        if (_scrim.Color.A != 0.28f * pa) _scrim.Color = new Color(UiKit.Ink, 0.28f * pa);
        _scrim.Visible = pa > 0f;
        // The bookmark, bottom right beside the pause control: shown while zoomed in.
        if (t.InRiverAge != _achRiver) AchAge(t);
        // n10 (research Q9): achievements unlock only inside a step (Town.CheckAchievements, CheckRiverAchievements), so
        // the count is taken again only when the tick, the town or the age changed, not every frame (it read every
        // achievement's value, and a second-age one builds a string).
        if (t != _achCountTown || t.Tick != _achCountTick || _achRiver != _achCountRiver)
        {
            _achCountTown = t; _achCountTick = t.Tick; _achCountRiver = _achRiver;
            _achGot = _achRiver ? TownSummary.RiverAchieved(t) : TownSummary.Achieved(t);
        }
        int got = _achGot;
        int sig = got + 1000 * Settings.GameInput.Version + (_achRiver ? 500 : 0);
        if (sig != _achSig)
        {
            _achSig = sig;
            _achClose.Text = "Close" + Settings.GameInput.Tag("tt_achievements");
            _paveHelp = null;
        }
        if (_fullA < 0.5f) { if (AchievementsOpen) CloseAchievements(); if (AgePageOpen) HidePage(_ageFade, false); }
        if (_agePanel.Visible) Centre(_agePanel);
        if (!_achPanel.Visible) return;
        Centre(_achPanel);
        _achClock += dt;
        if (_achClock < 0.5) return;
        _achClock = 0;
        int n = Math.Min(AchCount(t, _achRiver), _achBuiltCount);
        for (int i = 0; i < n; i++)
        {
            var a = Ach(t, _achRiver, i);
            string st = AchievementState(a);
            if (_achState[i].Text != st) _achState[i].Text = st;
            _achName[i].Modulate = a.Unlocked ? Colors.White : new Color(1, 1, 1, 0.75f);
            _achIcon[i].Modulate = a.Unlocked ? Colors.White : new Color(1, 1, 1, 0.3f);
            var sz = _achBack[i].Size;
            float k = (float)Math.Clamp(a.Unlocked ? 1 : a.Progress, 0, 1);
            _achFill[i].Position = Vector2.One;
            _achFill[i].Size = new Vector2(Math.Max(0, (sz.X - 2) * k), Math.Max(0, sz.Y - 2));
            _achFill[i].Color = a.Unlocked ? UiKit.Moss : UiKit.RiverBlue;
        }
        if (got != _achShown)
        {
            _achShown = got;
            _achHead.Text = _achRiver
                ? $"Achievements of the river, {got} of {t.RiverAchievementCount}. The first age: {TownSummary.Achieved(t)} of {t.AchievementCount}"
                : $"Achievements, {got} of {t.AchievementCount}";
        }
    }

    private string? _paveHelp, _extendHelp;
    /// <summary>The Pave and Extend banners' help line with the keys as bound now.</summary>
    private string ModeHelp(bool pave)
    {
        if (_paveHelp == null)
        {
            string esc = Settings.GameInput.Bound("tt_pause");
            string Stop(string own) => own.Length > 0 && esc.Length > 0 ? $" {own} or {esc} to stop." : esc.Length > 0 ? $" {esc} to stop." : "";
            _paveHelp = "Pave: click a lit road. Hold Shift to pave several." + Stop(Settings.GameInput.Bound("tt_pave"));
            _extendHelp = "Extend: click a lit cottage. Hold Shift to extend several." + Stop(StopKey("tt_buy_5", 1).Replace(" to stop", ""));
        }
        return pave ? _paveHelp : _extendHelp!;
    }

    private void Centre(Control c)
    {
        c.ResetSize();
        var z = Zones.Page;
        c.Position = new Vector2(z.Position.X, MathF.Floor((_built.Y - c.Size.Y) / 2f)) + new Vector2(0, PageDrop(c));
    }
}
