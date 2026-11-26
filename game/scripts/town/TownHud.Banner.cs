using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Round 3: a level reached is a banner of its own (night 3: in the Message zone at the top centre, one at a time; the
/// camera follows the town to show it). The "centre the view" control is in navigation (TownHud.Timers.cs).
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
        // Night 3: the Message zone, 96 high, accent card; a title and up to two caption lines.
        var z = Zones;
        _banner = new PanelContainer { Name = "Banner", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _banner.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Accent, 0));
        _banner.Position = z.Banner.Position;
        _banner.Size = z.Banner.Size;
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _banner.AddChild(holder);
        int pad = UiKit.U(_s, 12);
        _bannerHead = UiKit.Fixed(_s, UiKit.Title, UiKit.Serif, UiKit.Ink, HorizontalAlignment.Center);
        _bannerHead.Name = "Title";
        _bannerHead.Position = new Vector2(pad, UiKit.U(_s, 8));
        _bannerHead.Size = new Vector2(z.Banner.Size.X - 2 * pad, UiKit.U(_s, 30));
        _bannerText = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.Ink, HorizontalAlignment.Center);
        _bannerText.Name = "Text";
        _bannerText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bannerText.MaxLinesVisible = 2;
        _bannerText.VerticalAlignment = VerticalAlignment.Top;
        _bannerText.Position = new Vector2(pad, UiKit.U(_s, 40));
        _bannerText.Size = new Vector2(z.Banner.Size.X - 2 * pad, UiKit.U(_s, 46));
        holder.AddChild(_bannerHead);
        holder.AddChild(_bannerText);
        _root.AddChild(_banner);
        _bannerAge = -1;
    }

    public void Banner(string head, string text)
    {
        string line = head + ": " + text;
        Log(BannerLog, line);
        Log(NoteLog, line);
        LastNote = line;
        _bannerAt = null;
        if (BannerWaits(head, text)) return;
        _pendingHead = ""; _pendingText = ""; // the newest banner wins
        ShowBanner(head, text);
    }

    /// <summary>One banner at a time: a new one takes the zone at once (its text swapped, its time from the start).</summary>
    private void ShowBanner(string head, string text)
    {
        _bannerHead.Text = head;
        _bannerText.Text = text;
        _bannerAge = 0;
        _banner.Visible = _fullA > 0.01f;
    }

    private void BannerFrame(float dt)
    {
        PendingBannerFrame();
        if (_bannerAge < 0) { _banner.Visible = false; return; }
        _bannerAge += dt;
        if (_bannerAge > BannerSeconds) { _bannerAge = -1; _banner.Visible = false; return; }
        _banner.Visible = _fullA > 0.01f && !_drv.Town.ChoiceOpen;
        float a = Math.Clamp(Math.Min(_bannerAge / 0.3f, (BannerSeconds - _bannerAge) / 1f), 0f, 1f) * _fullA;
        _banner.Modulate = new Color(1, 1, 1, a);
    }
}
