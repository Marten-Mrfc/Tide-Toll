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
    private PageLeaf _wipe = null!;
    private float _wipeT = -1;
    private int _tideSig = int.MinValue;
    private bool _titleRiver, _sealPage;
    private Texture2D? _iconHigh, _iconLow;

    /// <summary>For tests: the tide dial as shown, whether it is visible, its needle (0..1 of a cycle), the page turning.</summary>
    public string TideText => _tide.Visible ? _tideText.Text : "";
    public bool TideVisible => _tide.Visible;
    public float TideNeedle { get; private set; }
    public bool PageTurning => _wipeT >= 0;
    /// <summary>
    /// Round 7: the turning page covers the whole screen from half way: what the new age changes on screen (the right
    /// page's title, the river, the tide dial) changes only from then on, under the leaf.
    /// </summary>
    public bool PageCovering => _wipeT < 0 || _wipeT >= WipeSeconds * 0.5f;
    /// <summary>The age-change leaf's moving edge on screen (canvas x) and whether it is drawn (tests and frame strips).</summary>
    public float PageLeafEdge => _wipe.Edge;
    public bool PageLeafShown => _wipe.Visible;
    /// <summary>Frame strips: the page turn stands at this many seconds in (negative lets it run).</summary>
    public float HoldPageTurnAt = -1f;
    public bool SealPageShown => AgePageOpen && _sealPage;

    private void BuildRiverParts()
    {
        _iconHigh = IconOr("ui-tide-high", "ui-boat");
        _iconLow = IconOr("ui-tide-low", "ui-boat");
        // Night 3: the tide in the timers zone (bottom left, beside the bell): the dial, "High, falling", "low in 1:14".
        _tide = new PanelContainer { Name = "Tide", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _tide.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _tide.AddChild(holder);
        _tideIcon = UiKit.IconRect(_s, _iconHigh);
        _tideIcon.Visible = false;
        holder.AddChild(_tideIcon);
        _tideDial = new Control { Name = "Dial", MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(UiKit.U(_s, 6), UiKit.U(_s, 6)), Size = Vector2.One * UiKit.U(_s, 36) };
        _tideDial.Draw += DrawDial;
        holder.AddChild(_tideDial);
        _tideText = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont);
        _tideText.Name = "State";
        _tideText.AutowrapMode = TextServer.AutowrapMode.Off;
        _tideText.MaxLinesVisible = 2;
        _tideText.Position = new Vector2(UiKit.U(_s, 48), UiKit.U(_s, 2));
        _tideText.Size = new Vector2(Zones.Tide.Size.X - UiKit.U(_s, 52), UiKit.U(_s, 44));
        holder.AddChild(_tideText);
        _root.AddChild(_tide);
        _tideSig = int.MinValue;

        // Round 7: an opaque leaf of the book's paper with a curled, shaded edge and its shadow (PageLeaf), not a
        // see-through rectangle: nothing behind it shows through.
        _wipe = new PageLeaf { Name = "PageTurn", Opaque = true, S = _s, Visible = false };
        _wipe.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_wipe);
        if (_wipeT >= 0) { PlaceWipe(); _root.MoveChild(_wipe, _root.GetChildCount() - 1); }
    }

    /// <summary>
    /// The leaf sweeps in from the right edge (its left edge moving, the curl and its shadow on that side) until it covers
    /// the whole screen half way, then slides on off to the left (its right edge moving). Eased in and out; its shading
    /// grows from nothing and goes back to nothing, so it neither appears nor leaves in one frame.
    /// </summary>
    private void PlaceWipe()
    {
        float k = Math.Clamp(_wipeT / WipeSeconds, 0f, 1f);
        float e = BookMotion.Smoother(k);
        float w = _built.X, x = w * (1f - 2f * e);
        float lift = Math.Clamp(4f * Math.Min(e, 1f - e), 0f, 1f);
        var area = new Rect2(0, 0, w, _built.Y);
        // First half: the leaf hangs from the right edge and its left edge comes in; then it is held at the left edge
        // and its right edge goes out.
        if (x >= 0) _wipe.Place(area, w + 2f, x, lift);
        else _wipe.Place(area, -2f, w + x, lift);
        if (!_wipe.Visible && k < 1f) _wipe.Visible = true;
        // Over every card, also those the new age builds again while the leaf covers them.
        int last = _root.GetChildCount() - 1;
        if (_wipe.GetIndex() != last) _root.MoveChild(_wipe, last);
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
        // Over every card of the interface (some are made after it).
        _root.MoveChild(_wipe, _root.GetChildCount() - 1);
        _main.Audio.PageTurn();
        // The title changes under the leaf (RiverFrame), once it covers the page.
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
        if (_wipeT >= 0)
        {
            _wipeT = HoldPageTurnAt >= 0 ? Math.Min(HoldPageTurnAt, WipeSeconds) : _wipeT + Math.Min(dt, BookMotion.MaxStep);
            PlaceWipe();
            if (_wipeT >= WipeSeconds) { _wipeT = -1; _wipe.Visible = false; }
        }
        if (t.InRiverAge != _titleRiver && PageCovering) SetPageTitle(t.InRiverAge);
        bool show = t.InRiverAge && _fullA > 0.01f && PageCovering;
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
            string lvl = t.HighWater ? "High" : t.FordOpen ? "Low" : t.TideLevel >= 0.5 ? "High" : "Low";
            _tideText.Text = t.TideRising ? $"{lvl}, rising\nhigh in {secs / 60}:{secs % 60:00}" : $"{lvl}, falling\nlow in {secs / 60}:{secs % 60:00}";
            _ = state;
            _tideIcon.Texture = t.TideLevel >= 0.5 ? _iconHigh : _iconLow;
            _tideDial.QueueRedraw();
        }
        else if (_drv.StepsRun % 10 == 0) _tideDial.QueueRedraw();
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
        _req.Modulate = Colors.White;
        int secs = (int)Math.Ceiling(v.Patience);
        int sig = HashCode.Combine(v.Id, v.Loaded, v.Want, secs, (int)v.Phase, 77);
        if (sig != _reqSig)
        {
            _reqSig = sig;
            _reqWho.Text = $"Request from the skipper {v.Skipper}";
            _reqAsk.Text = $"{v.Want} {GoodName(v.Good)} at the landing";
            _reqDone.Text = v.Phase == BoatPhase.Moored ? $"Loaded {v.Loaded} of {v.Want}, leaves in {secs} s" : "Waiting for a berth";
            _reqReward.Text = $"Pays {Money(t.BoatUnitPrice(v.Good))} a unit, more when filled";
            _reqDone.AddThemeColorOverride("font_color", UiKit.MutedNumber);
            _reqTintDirty = true;
        }
        float k = (float)Math.Clamp(v.PatienceMax > 0 ? v.Patience / v.PatienceMax : 0, 0, 1);
        SetGoalBar(v.Want > 0 ? Math.Min(v.Loaded, v.Want) / (float)v.Want : 0, k, _reqDone, UiKit.MutedNumber);
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
        ShowPage(_ageFade);
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
