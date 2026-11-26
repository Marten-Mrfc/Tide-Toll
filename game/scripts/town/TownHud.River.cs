using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 4, the second age in the interface: the tide dial (high or low water, the time to the turn, a needle on a
/// small dial), the ringing-in moment (a page turns over the town, the right page's title becomes the town on the
/// river), the second age's page ("The second age: The River", what is new), the borough seal's page (the second age
/// is complete, what the town achieved in both ages, more ages are planned but not in the game), skippers' requests
/// in the request card, coin numbers for landing deliveries and tolls, and the river's hint first on the hint line.
/// </summary>
public partial class TownHud
{
    public const string RiverTitle = "The Town on the River";
    public const float WipeSeconds = 1.1f;
    private PanelContainer _tide = null!;
    private TextureRect _tideIcon = null!;
    private Label _tideText = null!;
    private Control _tideDial = null!;
    private ColorRect _wipe = null!, _wipeEdge = null!;
    private float _wipeT = -1;
    private int _tideSig = int.MinValue;
    private bool _titleRiver, _sealPage;
    private Texture2D? _iconHigh, _iconLow;

    /// <summary>For tests: the tide dial as shown, whether it is visible, its needle (0..1 of a cycle), the page turning.</summary>
    public string TideText => _tide.Visible ? _tideText.Text : "";
    public bool TideVisible => _tide.Visible;
    public float TideNeedle { get; private set; }
    public bool PageTurning => _wipeT >= 0;
    public bool SealPageShown => AgePageOpen && _sealPage;

    private void BuildRiverParts()
    {
        _iconHigh = IconOr("ui-tide-high", "ui-boat");
        _iconLow = IconOr("ui-tide-low", "ui-boat");
        _tide = new PanelContainer { Name = "Tide", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.95f), 1.2f);
        box.SetContentMarginAll(UiKit.Px(_s, 7));
        _tide.AddThemeStyleboxOverride("panel", box);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiKit.Px(_s, 8));
        _tide.AddChild(row);
        _tideIcon = new TextureRect
        {
            Texture = _iconHigh, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = Vector2.One * UiKit.Px(_s, 34), MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        row.AddChild(_tideIcon);
        _tideDial = new Control { Name = "Dial", CustomMinimumSize = Vector2.One * UiKit.Px(_s, 40), MouseFilter = Control.MouseFilterEnum.Ignore };
        _tideDial.Draw += DrawDial;
        row.AddChild(_tideDial);
        _tideText = UiKit.Text("", _s, 16, UiKit.UiBold);
        row.AddChild(_tideText);
        _tide.TooltipText = "The tide. Boats come at high water; the ford can be walked at low water.";
        _root.AddChild(_tide);
        _tideSig = int.MinValue;

        _wipe = new ColorRect { Name = "PageTurn", Color = new Color(UiKit.Paper, 0.97f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _wipeEdge = new ColorRect { Name = "PageEdge", Color = new Color(Palette.Ink, 0.35f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _root.AddChild(_wipe);
        _root.AddChild(_wipeEdge);
    }

    /// <summary>The dial: a ring with high water at the top and low at the foot, the needle at the tide now.</summary>
    private void DrawDial()
    {
        var sz = _tideDial.Size;
        var c = sz / 2f;
        float r = Math.Min(sz.X, sz.Y) / 2f - 2f;
        _tideDial.DrawCircle(c, r, new Color(Palette.River, 0.25f));
        _tideDial.DrawArc(c, r, 0, MathF.Tau, 32, Palette.Ink, Math.Max(1f, UiKit.Px(_s, 1.5f)), true);
        _tideDial.DrawLine(c + new Vector2(0, -r), c + new Vector2(0, -r + UiKit.Px(_s, 5)), Palette.Ink, 2f);
        _tideDial.DrawLine(c + new Vector2(0, r), c + new Vector2(0, r - UiKit.Px(_s, 5)), Palette.Ink, 2f);
        // Needle angle: 0 at the top (high water), half a turn at the foot (low water).
        float a = TideNeedle * MathF.Tau;
        var tip = c + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * (r - UiKit.Px(_s, 4));
        _tideDial.DrawLine(c, tip, Palette.Brick, Math.Max(2f, UiKit.Px(_s, 2.5f)), true);
        _tideDial.DrawCircle(c, UiKit.Px(_s, 2.5f), Palette.Ink);
    }

    /// <summary>The next age is rung in: the page turns over the town and the right page takes the river's title.</summary>
    private void RiverRung()
    {
        _wipeT = 0;
        _main.Audio.PageTurn();
        SetPageTitle(true);
    }

    private void SetPageTitle(bool river)
    {
        _titleRiver = river;
        if (_main.Book.GetNodeOrNull<Label3D>("RightPage/MapTitle") is { } title)
            title.Text = river ? RiverTitle : "A Plan of the Hamlet";
    }

    /// <summary>The right page's title as drawn (tests).</summary>
    public string PageTitle => _main.Book.GetNodeOrNull<Label3D>("RightPage/MapTitle")?.Text ?? "";

    private void RiverFrame(float dt)
    {
        var t = _drv.Town;
        if (t.InRiverAge != _titleRiver) SetPageTitle(t.InRiverAge);
        if (_wipeT >= 0)
        {
            _wipeT += dt;
            float k = Math.Clamp(_wipeT / WipeSeconds, 0f, 1f);
            // A page sweeps over from the right edge to the left and away, its edge shadowed.
            float e = k * k * (3f - 2f * k);
            float w = _built.X;
            float x = w * (1f - 2f * e);
            _wipe.Visible = _wipeEdge.Visible = k < 1f;
            _wipe.Position = new Vector2(Math.Max(0, x), 0);
            _wipe.Size = new Vector2(Math.Max(0, Math.Min(w, w - x) - Math.Max(0, -x)), _built.Y);
            _wipeEdge.Position = new Vector2(x - UiKit.Px(_s, 10), 0);
            _wipeEdge.Size = new Vector2(UiKit.Px(_s, 10), _built.Y);
            if (k >= 1f) _wipeT = -1;
        }
        bool show = t.InRiverAge && _fullA > 0.01f;
        _tide.Visible = show;
        if (!show) return;
        _tide.Modulate = new Color(1, 1, 1, _fullA);
        // Needle: top (0) at high water, going round through low (0.5) and back.
        TideNeedle = (float)(t.TideRising ? 1.0 - 0.5 * t.TideLevel : 0.5 * (1.0 - t.TideLevel));
        int secs = (int)Math.Ceiling(t.TideSecondsToTurn);
        int sig = HashCode.Combine(secs, t.HighWater, t.FordOpen, t.TideRising, Settings.GameInput.Version);
        if (sig != _tideSig)
        {
            _tideSig = sig;
            string state = t.HighWater ? "High water" : t.FordOpen ? "Low water" : t.TideRising ? "The tide is rising" : "The tide is falling";
            _tideText.Text = t.TideRising ? $"{state}\nrising, highest in {secs / 60}:{secs % 60:00}" : $"{state}\nfalling, lowest in {secs / 60}:{secs % 60:00}";
            _tideIcon.Texture = t.TideLevel >= 0.5 ? _iconHigh : _iconLow;
            _tideDial.QueueRedraw();
        }
        else if (_drv.StepsRun % 10 == 0) _tideDial.QueueRedraw();
        _tide.ResetSize();
        var led = _ledger.GetRect();
        _tide.Position = new Vector2(led.End.X + UiKit.Px(_s, 10), led.Position.Y);
    }

    /// <summary>River events in the interface: coin numbers at the landing and the toll, the seal's moment.</summary>
    private void RiverEvent(in TownEvent e)
    {
        bool coins = Settings.GameSettings.Current?.ShowCoinNumbers ?? true;
        switch (e.Kind)
        {
            case TownEventKind.LandingDelivery: if (coins) Float(e.X, e.Y, e.Coin); break;
            case TownEventKind.TollPaid: if (coins) Float(e.X, e.Y, e.Coin); break;
            case TownEventKind.BoatArrived:
                if (e.Value > 0)
                    for (int i = 0; i < _drv.Town.BoatCount; i++)
                        if (_drv.Town.Boat(i).Id == e.Index) { string s = _drv.Town.BoatRequestText(i); if (s.Length > 0) Note(s + ".", true); }
                break;
            case TownEventKind.Seal:
                Banner("The borough seal", "The town is a borough. The second age is complete.");
                _agePageIn = AgePageDelay;
                break;
        }
    }

    /// <summary>A skipper's request in the request card while no town request runs. Returns true when shown.</summary>
    private bool RiverRequestFrame()
    {
        var t = _drv.Town;
        if (!t.InRiverAge || _fullA <= 0.01f) return false;
        int best = -1;
        for (int i = 0; i < t.BoatCount; i++)
        {
            var b = t.Boat(i);
            if (b.Phase is BoatPhase.Gone or BoatPhase.Leaving || !b.Request) continue;
            if (best < 0 || b.Phase == BoatPhase.Moored) best = i;
        }
        if (best < 0) return false;
        var v = t.Boat(best);
        _req.Visible = true;
        _req.Position = new Vector2(UiKit.Px(_s, 16), (_ledger != null ? _ledger.GetRect().End.Y : 180) + UiKit.Px(_s, 8));
        _req.Modulate = new Color(1, 1, 1, _fullA);
        _reqBox.BgColor = new Color(UiKit.Paper, 0.96f);
        int secs = (int)Math.Ceiling(v.Patience);
        int sig = HashCode.Combine(v.Id, v.Loaded, v.Want, secs, (int)v.Phase, 77);
        if (sig != _reqSig)
        {
            _reqSig = sig;
            _reqWho.Text = $"Request from the skipper {v.Skipper}";
            _reqAsk.Text = $"{v.Want} {GoodName(v.Good)} at the landing";
            _reqDone.Text = v.Phase == BoatPhase.Moored ? $"Loaded {v.Loaded} of {v.Want}, leaves in {secs} s" : "Waiting for a berth";
            _reqReward.Text = $"Pays {Money(t.BoatUnitPrice(v.Good))} coin a unit, more when filled";
        }
        float k = (float)Math.Clamp(v.PatienceMax > 0 ? v.Patience / v.PatienceMax : 0, 0, 1);
        var sz = _reqBarBack.Size;
        _reqBar.Position = new Vector2(1, 1);
        _reqBar.Size = new Vector2(Math.Max(0, (sz.X - 2) * k), Math.Max(0, sz.Y - 2));
        _reqBar.Color = k < 0.25f ? UiKit.Warn : UiKit.Focus;
        return true;
    }

    /// <summary>The page after the ring (the second age and what is new) or after the seal (the age complete).</summary>
    private void OpenRiverPage()
    {
        var t = _drv.Town;
        string name = TownName.Of(t.Seed);
        _sealPage = t.HasSeal;
        if (_sealPage)
        {
            long mins = (long)(t.Time / 60.0), river = (long)((t.Time - Math.Max(0, t.RiverStartTime)) / 60.0);
            _ageTitle.Text = "The second age is complete";
            _ageText.Text =
                $"{name} is a borough, with its seal. In {Hours(mins)} it grew from one cottage and a well to {TownSummary.Cottages(t) + RiverCottages(t)} cottages " +
                $"on both banks and {t.VillagerCount} villagers.\n\n" +
                $"First age: the charter, {TownSummary.OpenRoads(t)} roads, {t.CountPaved()} of them paved, requests met: {t.RequestsMet} of {t.RequestsAsked}.\n" +
                $"Second age, {Hours(river)}: {t.BoatsCome} boats came, {t.BoatsFilled} left full. Landing: {Money(t.LandingCoin)} coin. " +
                $"Tolls: {t.Tolls}, {Money(t.TollCoin)} coin. Wool and cloth at the market: {Money(t.RiverMarketCoin)} coin.\n" +
                $"Coin earned in all: {Money(t.CoinEarned)}. Achievements: {TownSummary.Achieved(t)} of {t.AchievementCount} and {RiverAchieved(t)} of {t.RiverAchievementCount} on the river.\n\n" +
                "More ages are planned, but they are not in the game yet. Your town stays as it is, and you can keep playing it.";
        }
        else
        {
            _ageTitle.Text = "The second age: The River";
            _ageText.Text =
                $"{name} has its charter and the right to trade on the river. Nothing of the town is lost.\n\n" +
                "New in this age:\n" +
                "Boats come up the river at high water and moor at the landing. Each wants one good: carry it to the landing. A full boat pays a bonus.\n" +
                "The tide: watch the dial. Boats come at high water; the ford can be walked at low water.\n" +
                "Wool from the pasture on the far bank, and Cloth from the weaver.\n" +
                "Crossings, one after another: the ford, the ferry, the footbridge, and the drawbridge that takes a toll.\n\n" +
                "The age ends with the borough seal.";
        }
        _ageClose.Text = "Keep playing" + Settings.GameInput.Tag("tt_pause");
        _agePanel.Visible = true;
        _main.Audio.PageTurn();
    }

    private static string Hours(long mins) => mins >= 60 ? $"{mins / 60} h {mins % 60} min" : $"{mins} min";

    public static int RiverCottages(Town t)
    {
        if (!t.InRiverAge) return 0;
        int n = 0;
        var rp = t.RiverPlan!;
        for (int i = 0; i < rp.Plots.Length; i++) if (rp.Plots[i].Kind == RiverPlotKind.Cottage && t.RiverPlotGiven(i)) n++;
        return n;
    }

    public static int RiverAchieved(Town t)
    {
        int n = 0;
        for (int i = 0; i < t.RiverAchievementCount; i++) if (t.RiverAchievement(i).Unlocked) n++;
        return n;
    }
}
