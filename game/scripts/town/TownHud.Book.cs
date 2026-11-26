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
            _achCell[i].TooltipText = a.Description;
        }
        _achShown = -1;
    }
    private Button _achMark = null!, _achClose = null!, _ageClose = null!;
    private int _achShown = -1, _achSig = -1;
    private double _achClock;
    private float _agePageIn = -1;

    public bool AchievementsOpen => _achPanel.Visible;
    public bool AgePageOpen => _agePanel.Visible;
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
        _achMark = MakeButton("Achievements", "ui-achievement", "ui-reputation", 15, 6);
        _achMark.Pressed += ToggleAchievements;
        _root.AddChild(_achMark);

        _achPanel = Page("AchievementsPage", 1120);
        var col = (VBoxContainer)_achPanel.GetChild(0);
        _achHead = UiKit.Text("Achievements", _s, 32, UiKit.Serif);
        _achHead.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_achHead);
        col.AddChild(UiKit.Rule(_s, 0.6f));
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", UiKit.Px(_s, 34));
        grid.AddThemeConstantOverride("v_separation", UiKit.Px(_s, 5));
        col.AddChild(grid);
        int n = Math.Min(_drv.Town.AchievementCount, _achName.Length);
        for (int i = 0; i < n; i++)
        {
            var a = _drv.Town.Achievement(i);
            var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(UiKit.Px(_s, 500), 0) };
            cell.AddThemeConstantOverride("separation", 0);
            cell.TooltipText = a.Description;
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            _achName[i] = UiKit.Text(a.Name, _s, 16, UiKit.UiBold);
            _achName[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _achState[i] = UiKit.Text("", _s, 15, UiKit.UiFont, UiKit.FadedInk);
            row.AddChild(_achName[i]);
            row.AddChild(_achState[i]);
            cell.AddChild(row);
            var desc = UiKit.Text(a.Description, _s, 13, UiKit.UiFont, UiKit.FadedInk);
            cell.AddChild(desc);
            _achDesc[i] = desc;
            _achCell[i] = cell;
            var back = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 5)) };
            var bs = UiKit.Card(_s, UiKit.PaperDark, 0.6f);
            bs.SetContentMarginAll(0);
            back.AddThemeStyleboxOverride("panel", bs);
            _achFill[i] = new ColorRect { Color = Palette.Thatch, MouseFilter = Control.MouseFilterEnum.Ignore };
            back.AddChild(_achFill[i]);
            _achBack[i] = back;
            cell.AddChild(back);
            grid.AddChild(cell);
        }
        _achBuiltCount = n;
        AchAge(_drv.Town);
        _achClose = new Button { Name = "Close", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _achClose.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, 16));
        _achClose.Pressed += CloseAchievements;
        col.AddChild(_achClose);

        _agePanel = Page("FirstAgePage", 820);
        var ac = (VBoxContainer)_agePanel.GetChild(0);
        _ageTitle = UiKit.Text("The first age is complete", _s, 40, UiKit.Display);
        _ageTitle.HorizontalAlignment = HorizontalAlignment.Center;
        ac.AddChild(_ageTitle);
        ac.AddChild(UiKit.Rule(_s, 0.6f));
        _ageText = UiKit.Text("", _s, 19, UiKit.Serif);
        _ageText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _ageText.CustomMinimumSize = new Vector2(UiKit.Px(_s, 740), 0);
        ac.AddChild(_ageText);
        _ageClose = new Button { Name = "KeepPlaying", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _ageClose.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, 18));
        _ageClose.Pressed += CloseAgePage;
        ac.AddChild(_ageClose);
        _achShown = -1; _achSig = -1;
    }

    /// <summary>A page of the book over the town: paper with a double ink edge, centred.</summary>
    private PanelContainer Page(string name, float width)
    {
        var p = new PanelContainer { Name = name, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.985f), 2.5f);
        box.SetContentMarginAll(UiKit.Px(_s, 22));
        box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(_s, 34);
        p.AddThemeStyleboxOverride("panel", box);
        p.CustomMinimumSize = new Vector2(UiKit.Px(_s, width), 0);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 10));
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
        _achPanel.Visible = true;
        _achClock = 1e9;
        _main.Audio.UiTab();
    }

    public void CloseAchievements()
    {
        if (_achPanel == null || !_achPanel.Visible) return;
        _achPanel.Visible = false;
        _main.Audio.UiTab();
    }

    public void CloseAgePage()
    {
        if (_agePanel == null || !_agePanel.Visible) return;
        _agePanel.Visible = false;
        _main.Audio.PageTurn();
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
            Banner("A new age", "The bell rings in the next age.");
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
            _main.Rig.LookAt(c, Math.Max(_main.Rig.MinSize, Math.Min(_main.Rig.Camera.Size * 0.7f, _main.Rig.PlaySize * 0.6f)), instant: false);
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
        _agePanel.Visible = true;
        _main.Audio.PageTurn();
    }

    private void BookFrame(float dt)
    {
        var t = _drv.Town;
        if (_agePageIn > 0) { _agePageIn -= dt; if (_agePageIn <= 0) OpenAgePage(); }
        // The bookmark, bottom right beside the pause control: shown while zoomed in.
        if (t.InRiverAge != _achRiver) AchAge(t);
        int got = _achRiver ? TownSummary.RiverAchieved(t) : TownSummary.Achieved(t);
        int sig = got + 1000 * Settings.GameInput.Version + (_achRiver ? 500 : 0);
        if (sig != _achSig)
        {
            _achSig = sig;
            _achMark.Text = $"Achievements{Settings.GameInput.Tag("tt_achievements", "   ")}\n{got} of {AchCount(t, _achRiver)}{(_achRiver ? " on the river" : "")}";
            _achClose.Text = "Close" + Settings.GameInput.Tag("tt_achievements");
            PauseMark.Text = "Pause" + Settings.GameInput.Tag("tt_pause");
            _home.Text = "Back to the town" + Settings.GameInput.Tag("tt_home_view");
            _paveHelp = null;
        }
        // Round 4: the pause control sits bottom right while zoomed in; once the page or the book is in view it moves up
        // beside the slim strip, clear of the book's corner and its page number.
        PauseMark.ResetSize();
        float pm = UiKit.Px(_s, 16);
        PauseMark.Position = _slim.Visible && _fullA < 0.5f
            ? new Vector2(_slim.Position.X + _slim.Size.X + UiKit.Px(_s, 14), _slim.Position.Y + (_slim.Size.Y - PauseMark.Size.Y) / 2f)
            : new Vector2(_built.X - PauseMark.Size.X - pm, _built.Y - PauseMark.Size.Y - pm);
        _achMark.Visible = _fullA > 0.01f && !AgePageOpen;
        _achMark.Modulate = new Color(1, 1, 1, _fullA);
        _achMark.ResetSize();
        _achMark.Position = new Vector2(PauseMark.Position.X - _achMark.Size.X - UiKit.Px(_s, 10), _built.Y - _achMark.Size.Y - UiKit.Px(_s, 16));
        if (_fullA < 0.5f) { if (AchievementsOpen) CloseAchievements(); if (AgePageOpen) _agePanel.Visible = false; }
        if (AgePageOpen) Centre(_agePanel);
        if (!AchievementsOpen) return;
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
            var sz = _achBack[i].Size;
            float k = (float)Math.Clamp(a.Unlocked ? 1 : a.Progress, 0, 1);
            _achFill[i].Position = Vector2.One;
            _achFill[i].Size = new Vector2(Math.Max(0, (sz.X - 2) * k), Math.Max(0, sz.Y - 2));
            _achFill[i].Color = a.Unlocked ? Palette.Thatch : new Color(Palette.Thatch, 0.55f);
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
            string stop = esc.Length > 0 ? $" {esc} to stop." : "";
            _paveHelp = "Pave: click a lit road. Hold Shift to pave several." + stop;
            _extendHelp = "Extend: click a lit cottage. Hold Shift to extend several." + stop;
        }
        return pave ? _paveHelp : _extendHelp!;
    }

    private void Centre(Control c)
    {
        c.ResetSize();
        c.Position = ((_built - c.Size) / 2f).Floor();
    }
}
