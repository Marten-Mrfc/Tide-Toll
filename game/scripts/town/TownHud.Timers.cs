using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, sections 2 and 3): the timers at the bottom left (the bell: icon, "Ring", its key; while
/// ringing the seconds over a bar running down; while resting the rest over a bar filling, disabled look) and
/// navigation at the bottom right (Achievements with its count, this age's page, centre the view, pause; each with
/// a tooltip naming it and its key; sheet v3: each shows its icon centred and nothing else, Achievements its icon and
/// count, no key cap). "Ring in the next age" lives in the ledger's strip until it is rung.
/// </summary>
public partial class TownHud
{
    private Button _bell = null!, _nextAge = null!, _agePageMark = null!;
    private Label _bellText = null!, _bellKey = null!, _nextAgeKey = null!;
    private Panel _bellBar = null!;
    private ColorRect _bellFill = null!;
    private int _bellSig = int.MinValue, _navSig = int.MinValue;
    private Label _achCount = null!;
    private string _bellTip = "";
    private int _achGot;

    public Button BellButton => _bell;
    public Button NextAgeButton => _nextAge;
    private readonly MessageFader _nextAgeF = new(LargeSeconds);
    /// <summary>The bell's whole sentence (its tooltip; tests): "Ringing: 7 s left, then rests 40 s".</summary>
    public string BellTipText => _bellTip;
    public Button AgePageMark => _agePageMark;

    private void BuildTimers()
    {
        var z = Zones;
        _bell = new Button { Name = "Bell", Text = "", ClipContents = true };
        UiKit.StyleButton(_bell, _s, 0);
        _bell.Position = z.Bell.Position; _bell.Size = z.Bell.Size;
        _bell.Pressed += RingBell;
        _bell.MouseEntered += () => _main.Audio.UiHover();
        var icon = UiKit.IconRect(_s, IconOr("nav-bell", "ui-bell"));
        icon.Position = new Vector2(UiKit.U(_s, 6), UiKit.U(_s, 12));
        _bell.AddChild(icon);
        _bellText = UiKit.Fixed(_s, UiKit.Body, UiKit.UiBold);
        _bellText.Name = "State";
        _bellText.Position = new Vector2(UiKit.U(_s, 34), UiKit.U(_s, 4));
        _bellText.Size = new Vector2(UiKit.U(_s, 88), UiKit.U(_s, 24));
        _bell.AddChild(_bellText);
        _bellKey = UiKit.KeyCap(_s);
        _bellKey.Position = new Vector2(UiKit.U(_s, 72), UiKit.U(_s, 6));
        _bellKey.Size = new Vector2(UiKit.U(_s, 50), UiKit.U(_s, 20));
        _bell.AddChild(_bellKey);
        _bellBar = UiKit.Bar(_s, UiKit.RiverBlue, out _bellFill);
        _bellBar.Name = "Bar";
        _bellBar.Position = new Vector2(UiKit.U(_s, 34), UiKit.U(_s, 32));
        _bellBar.Size = new Vector2(UiKit.U(_s, 86), UiKit.U(_s, 8));
        _bell.AddChild(_bellBar);
        _root.AddChild(_bell);
        _bellSig = int.MinValue;

        // The strip's button: Ring in the next age [N], accent fill, until it is rung.
        _nextAge = new Button { Name = "NextAge", Text = "Ring in the next age", Alignment = HorizontalAlignment.Left, Visible = false, ClipText = true };
        UiKit.StyleButton(_nextAge, _s, 8);
        _nextAge.AddThemeFontOverride("font", UiKit.UiBold);
        foreach (var (state, look) in new[] { ("normal", UiKit.ButtonLook.Normal), ("hover", UiKit.ButtonLook.Hover), ("pressed", UiKit.ButtonLook.Pressed), ("hover_pressed", UiKit.ButtonLook.Pressed) })
        {
            var b = UiKit.ButtonBox(_s, look, 8);
            b.BgColor = look == UiKit.ButtonLook.Hover ? UiKit.Wheat.Lerp(UiKit.Paper, 0.3f) : look == UiKit.ButtonLook.Pressed ? UiKit.Wheat.Darkened(0.1f) : UiKit.Wheat;
            _nextAge.AddThemeStyleboxOverride(state, b);
        }
        int pad = UiKit.U(_s, 12);
        _nextAge.Position = new Vector2(pad, UiKit.U(_s, 4));
        _nextAge.Size = new Vector2(_stripBox.Size.X - 2 * pad, UiKit.U(_s, 32));
        _nextAge.Pressed += NextAgePressed;
        _nextAgeKey = UiKit.KeyCap(_s);
        _nextAgeKey.Position = new Vector2(_nextAge.Size.X - UiKit.U(_s, 30), UiKit.U(_s, 6));
        _nextAgeKey.Size = new Vector2(UiKit.U(_s, 22), UiKit.U(_s, 20));
        _nextAgeKey.AddThemeColorOverride("font_color", UiKit.Ink);
        _nextAge.AddChild(_nextAgeKey);
        _stripBox.AddChild(_nextAge);
    }

    /// <summary>A navigation button: its icon (26) centred, nothing else (Achievements: icon at the left, the count).
    /// Disabled: border 1 at 40 percent ink, the icon at 40 percent (sheet v3).</summary>
    private Button NavButton(string name, int slot, string icon, string standIn)
    {
        var r = Zones.NavButton(slot);
        var b = new Button { Name = name, Text = "", ClipContents = true, Position = r.Position, Size = r.Size };
        UiKit.StyleButton(b, _s, 0);
        var off = UiKit.ButtonBox(_s, UiKit.ButtonLook.Disabled, 0);
        off.BorderColor = new Color(UiKit.Ink, 0.4f);
        b.AddThemeStyleboxOverride("disabled", off);
        b.MouseEntered += () => _main.Audio.UiHover();
        var ic = UiKit.IconRect(_s, IconOr(icon, standIn), 26);
        float side = UiKit.U(_s, 26);
        ic.Position = new Vector2(slot == 0 ? UiKit.U(_s, 8) : MathF.Round((r.Size.X - side) / 2f), MathF.Round((r.Size.Y - side) / 2f));
        b.AddChild(ic);
        _root.AddChild(b);
        return b;
    }

    private void BuildNav()
    {
        for (int i = 0; i < _dimWas.Length; i++) _dimWas[i] = -1; // new buttons: dim them afresh
        _achMark = NavButton("Achievements", 0, "nav-achievements", "ui-achievement");
        _achMark.Pressed += ToggleAchievements;
        _achCount = UiKit.Fixed(_s, UiKit.Body, UiKit.UiBold);
        _achCount.Name = "Count";
        _achCount.Position = new Vector2(UiKit.U(_s, 37), 0);
        _achCount.Size = new Vector2(_achMark.Size.X - UiKit.U(_s, 39), _achMark.Size.Y);
        _achMark.AddChild(_achCount);
        _agePageMark = NavButton("AgePage", 1, "nav-page", "ui-new-map");
        _agePageMark.Pressed += NextAgePressed;
        _home = NavButton("HomeMark", 2, "nav-home", "ui-reset");
        _home.Pressed += () => _main.Rig.HomePressed?.Invoke();
        PauseMark = NavButton("PauseMark", 3, "nav-pause", "ui-pause");
        PauseMark.Pressed += () => _main.Menu.Pause();
        _navSig = int.MinValue;
    }

    private void RingBell()
    {
        if (!_drv.Town.RingBell()) Refuse(_drv.Town.Level < 2 ? "The bell comes at level 2." : $"The bell is resting: {Math.Ceiling(_drv.Town.BellWait)} s.");
    }

    private void TimersFrame()
    {
        var t = _drv.Town;
        bool unlocked = t.Level >= 2 || t.BellRings > 0 || t.BellReady || t.BellWait > 0;
        int ringLeft = t.BellRinging ? Math.Max(1, (int)Math.Ceiling(t.BellWait - t.BellRest + t.BellDuration)) : 0;
        int restAfter = t.BellRinging ? Math.Max(0, (int)Math.Ceiling(t.BellRest - t.BellDuration)) : 0;
        int rest = (int)Math.Ceiling(t.BellWait);
        int sig = HashCode.Combine(unlocked, t.BellRinging, t.BellReady, ringLeft, restAfter, rest, Settings.GameInput.Version);
        if (sig != _bellSig)
        {
            _bellSig = sig;
            _bellKey.Text = Settings.GameInput.Bound("tt_bell");
            _bellKey.Visible = !t.BellRinging && t.BellReady;
            _bellText.Text = !unlocked ? "Level 2" : t.BellRinging ? $"{ringLeft} s" : t.BellReady ? "Ring" : $"rests {rest} s";
            _bellText.AddThemeFontSizeOverride("font_size", UiKit.Type(_s, t.BellReady || t.BellRinging ? UiKit.Body : UiKit.Caption));
            _bellTip = !unlocked ? "The bell comes at level 2." : t.BellRinging ? BellRingingText(ringLeft, restAfter) + "."
                : t.BellReady ? "Ring the bell: everyone hurries for a few seconds, then it rests." : $"The bell rests {rest} s more.";
            if (_bellKey.Text.Length > 0) _bellTip += $" Key: {_bellKey.Text}.";
        }
        _bell.Disabled = !t.BellReady;
        _bellBar.Visible = unlocked && !t.BellReady;
        if (_bellBar.Visible)
        {
            float k = t.BellRinging ? (float)(ringLeft / Math.Max(1.0, t.BellDuration)) : (float)(1.0 - t.BellWait / Math.Max(1.0, t.BellRest));
            UiKit.SetBar(_bellBar, _bellFill, k);
        }
        // Night 3, L2: the strip's button arrives and leaves by an eased fade (0.22 s), never in one frame.
        _nextAgeF.Want(t.CanRingNextAge && !t.NextAgeRung && !AgePageOpen);
        _nextAgeF.Step(_msgDt);
        bool nav = _nextAgeF.Value > 0f;
        if (_nextAge.Visible != nav) _nextAge.Visible = nav;
        if (nav) _nextAge.Modulate = new Color(1, 1, 1, _nextAgeF.Value);
        // Nav: keys and the count (text only on change).
        int got = _achGot; // counted once a frame by BookFrame (the count reads every achievement's value)
        int nsig = HashCode.Combine(got, Settings.GameInput.Version, _achRiver);
        if (nsig != _navSig)
        {
            _navSig = nsig;
            _achCount.Text = $"{got} of {AchCount(t, _achRiver)}";
            _nextAgeKey.Text = Settings.GameInput.Bound("tt_next_age");
        }
        _agePageMark.Disabled = !t.NextAgeRung;
        _home.Disabled = !_main.Rig.UserMoved;
        // Navigation stays at every zoom (sheet section 2); Achievements needs the town in view to open.
        _achMark.Disabled = _fullA < 0.5f;
        Dim(_agePageMark, 0); Dim(_home, 1); Dim(_achMark, 2);
    }

    /// <summary>A disabled navigation button shows it (sheet v3: its icon at 40 percent, with the button's border at 40
    /// percent ink), so Home reads as off while the view is already home.</summary>
    private readonly int[] _dimWas = { -1, -1, -1 };

    private void Dim(Button b, int i)
    {
        int now = b.Disabled ? 1 : 0;
        if (_dimWas[i] == now) return; // only when it changes (GetChildren makes an array)
        _dimWas[i] = now;
        float a = b.Disabled ? 0.4f : 1f;
        foreach (var ch in b.GetChildren())
            if (ch is CanvasItem ci && ci.Modulate.A != a) ci.Modulate = new Color(1, 1, 1, a);
    }
}
