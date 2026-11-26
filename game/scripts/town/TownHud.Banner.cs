using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Round 3: a level reached is a banner of its own (top middle, bigger and longer than a note, the gift named; the
/// camera follows the town to show it), and the "Back to the town [Home]" control that shows whenever the view is
/// not following the town.
/// </summary>
public partial class TownHud
{
    private PanelContainer _banner = null!;
    private Label _bannerHead = null!, _bannerText = null!;
    private float _bannerAge = -1;
    private Button _home = null!;
    private Vector3? _bannerAt;

    /// <summary>The banner's rectangle on screen while it shows (tests), and the gift point it keeps clear of.</summary>
    public Rect2 BannerRect => _banner.Visible ? _banner.GetGlobalRect() : new Rect2();
    public Vector2? BannerGiftScreen => _bannerAt is { } a ? _main.Rig.LocalToScreen(a) : null;

    /// <summary>Round 4: the banner keeps clear of this map point (the gift it announces) on screen.</summary>
    public void BannerClearOf(Vector3? local) => _bannerAt = local;

    /// <summary>The middle of what a level gives (its plots and roads), in map-local metres; null when it gives none.</summary>
    private Vector3? GiftPoint(int level)
    {
        var t = _drv.Town;
        if (level < 1 || level > t.Plan.Levels.Length) return null;
        var lv = t.Plan.Level(level);
        float x = 0, z = 0; int n = 0;
        foreach (int p in lv.Plots) { var c = PlotCentre(p); x += c.X; z += c.Z; n++; }
        foreach (int r in lv.Roads)
        {
            var road = t.Plan.Roads[r];
            road.At(road.Length / 2, out double rx, out double ry, out _);
            x += (float)rx * TownGround.Cell; z += (float)ry * TownGround.Cell; n++;
        }
        return n > 0 ? new Vector3(x / n, 0, z / n) : null;
    }

    /// <summary>The banner's text while it shows ("" otherwise), and every banner shown (tests).</summary>
    public string BannerText => _banner.Visible ? _bannerHead.Text + ": " + _bannerText.Text : "";
    public readonly System.Collections.Generic.List<string> BannerLog = new();
    public Button HomeMark => _home;

    private void BuildBanner()
    {
        _banner = new PanelContainer { Name = "Banner", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var box = UiKit.Card(_s, new Color(UiKit.Pressed, 0.97f), 2.5f);
        box.SetContentMarginAll(UiKit.Px(_s, 12));
        box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(_s, 28);
        _banner.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _banner.AddChild(col);
        _bannerHead = UiKit.Text("", _s, 34, UiKit.Serif);
        _bannerHead.HorizontalAlignment = HorizontalAlignment.Center;
        _bannerText = UiKit.Text("", _s, 20, UiKit.UiBold);
        _bannerText.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_bannerHead);
        col.AddChild(_bannerText);
        _root.AddChild(_banner);
        _bannerAge = -1;
    }

    public void Banner(string head, string text)
    {
        _bannerHead.Text = head;
        _bannerText.Text = text;
        _bannerAge = 0;
        _bannerAt = null;
        _banner.Visible = _fullA > 0.01f;
        string line = head + ": " + text;
        Log(BannerLog, line);
        Log(NoteLog, line);
        LastNote = line;
    }

    private void BannerFrame(float dt)
    {
        if (_bannerAge < 0) { _banner.Visible = false; return; }
        _bannerAge += dt;
        if (_bannerAge > BannerSeconds) { _bannerAge = -1; _banner.Visible = false; return; }
        _banner.Visible = _fullA > 0.01f;
        float a = Math.Clamp(Math.Min(_bannerAge / 0.3f, (BannerSeconds - _bannerAge) / 1f), 0f, 1f) * _fullA;
        _banner.Modulate = new Color(1, 1, 1, a);
        _banner.ResetSize();
        // Below the Pave or Extend banner when one shows.
        float y = UiKit.Px(_s, PaveMode || ExtendMode ? 70 : 18);
        // Round 4: never over the gift it announces: when the gift is under the banner's place (the camera is still
        // bringing it in, or the town lies high on the screen), the banner goes just below the gift instead.
        if (_bannerAt is { } at)
        {
            var g = _main.Rig.LocalToScreen(at);
            float pad = UiKit.Px(_s, 70);
            if (g.Y < y + _banner.Size.Y + pad && g.Y > -pad && MathF.Abs(g.X - _built.X / 2f) < _banner.Size.X / 2f + pad)
                y = Math.Min(g.Y + pad, _built.Y - _banner.Size.Y - UiKit.Px(_s, 120));
        }
        _banner.Position = new Vector2((_built.X - _banner.Size.X) / 2f, y);
    }

    private void BuildHomeMark()
    {
        _home = new Button { Name = "HomeMark", Text = "Back to the town  [Home]", FocusMode = Control.FocusModeEnum.None, Visible = false,
            TooltipText = "The view follows the town again" };
        _home.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, 16));
        _root.AddChild(_home);
        _home.Pressed += () => _main.Rig.HomePressed?.Invoke();
    }

    private void HomeFrame()
    {
        bool show = _main.Rig.UserMoved && _main.Stage == Main.ZoomStage.Town && _main.Menu != null && !_main.Menu.IsPaused && !PaveMode && !ExtendMode;
        _home.Visible = show;
        if (!show) return;
        _home.ResetSize();
        _home.Position = new Vector2((_built.X - _home.Size.X) / 2f, _built.Y - _home.Size.Y - UiKit.Px(_s, 16));
    }
}
