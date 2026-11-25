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
        _banner.Visible = _fullA > 0.01f;
        string line = head + ": " + text;
        BannerLog.Add(line);
        NoteLog.Add(line);
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
