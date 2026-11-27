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
        _iconHigh = IconOr("nav-tide-high", "ui-tide-high") ?? IconOr("ui-boat", "ui-boat");
        _iconLow = IconOr("nav-tide-low", "ui-tide-low") ?? IconOr("ui-boat", "ui-boat");
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
        // see-through rectangle: nothing behind it shows through. Last look: on a canvas layer of its own just over the
        // interface's (5), so every card (the shop, the bottom strip, cards built again under it) lies under it; the
        // new age's title is printed on its back.
        _wipeLayer = new CanvasLayer { Name = "PageTurnLayer", Layer = 6 };
        AddChild(_wipeLayer);
        _wipe = new PageLeaf { Name = "PageTurn", Opaque = true, S = _s, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _wipe.Position = Vector2.Zero; _wipe.Size = _built;
        _wipeLayer.AddChild(_wipe);
        _wipeTitle = UiKit.Fixed(_s, UiKit.DisplaySize, UiKit.Display, UiKit.Ink, HorizontalAlignment.Center);
        _wipeTitle.Name = "AgeTitle";
        _wipeTitle.ClipText = false;
        _wipeTitle.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        _wipeTitle.Text = RiverTitle;
        _wipeTitle.Size = new Vector2(_built.X, UiKit.U(_s, 80));
        _wipeTitle.PivotOffset = new Vector2(0, _wipeTitle.Size.Y / 2f);
        _wipeTitle.Visible = false;
        _wipeLayer.AddChild(_wipeTitle);
        if (_wipeT >= 0) PlaceWipe();
    }

    private CanvasLayer _wipeLayer = null!;
    private Label _wipeTitle = null!;

    /// <summary>
    /// Last look: the leaf turns about the book's spine, which lies off the left edge of the screen in the play view.
    /// First half: a leaf of the new age comes over from the spine and lies down on the screen (its free edge sweeping
    /// left to right, foreshortened, curled and shadowed as it falls), the new age's title on its back; it covers the
    /// whole screen at the middle (the river is built and the title changes under it). Second half: it is turned on
    /// over the spine (its free edge lifting from the right and going back to the left), and the new age lies under it.
    /// The projected edge is the leaf's width times the cosine of its angle, eased; no step at either end.
    /// </summary>
    private void PlaceWipe()
    {
        float k = Math.Clamp(_wipeT / WipeSeconds, 0f, 1f);
        // 0 standing at the spine, 1 lying flat over the screen, back to 0; held flat for a moment in the middle.
        float flat = k < 0.5f ? BookMotion.Smoother(Math.Min(1f, k / 0.46f)) : BookMotion.Smoother(Math.Min(1f, (1f - k) / 0.46f));
        float ang = (1f - flat) * MathF.PI / 2f;             // 90 degrees standing, 0 lying
        float w = _built.X + 4f, spine = -2f;
        float edge = spine + w * MathF.Cos(ang);
        float lift = Math.Max(0.04f, MathF.Sin(ang));
        bool on = k < 1f && (flat > 0f || k > 0f);
        var area = new Rect2(0, 0, _built.X, _built.Y);
        _wipe.From = k < 0.5f ? -1f : 1f;
        _wipe.Place(area, spine, edge, flat >= 1f ? 0.04f : lift);
        if (_wipe.Visible != on) _wipe.Visible = on;
        // The title on the leaf's back, squeezed with it toward the spine, clearest as it lies flat.
        float squeeze = Math.Max(0.001f, (edge - spine) / w);
        _wipeTitle.Scale = new Vector2(squeeze, 1f);
        _wipeTitle.Position = new Vector2(spine, _built.Y * 0.42f - _wipeTitle.Size.Y / 2f);
        float ta = Math.Clamp((flat - 0.5f) / 0.5f, 0f, 1f);
        _wipeTitle.Modulate = new Color(1, 1, 1, ta * ta * (3f - 2f * ta));
        bool tv = on && ta > 0f;
        if (_wipeTitle.Visible != tv) _wipeTitle.Visible = tv;
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
            if (_wipeT >= WipeSeconds) { _wipeT = -1; _wipe.Visible = false; _wipeTitle.Visible = false; }
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
                        if (_drv.Town.Boat(i).Id == e.Index) { string s = _drv.Town.BoatRequestText(i); if (s.Length > 0) Log(NoteLog, s + "."); }
                break;
            case TownEventKind.Seal:
                Large("The borough seal", "The town is a borough. The second age is complete.", PrioMoment, BannerLongStay, "seal-granted");
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
            // A large cargo's line may not fit the narrow rail: the shorter form, never cut (game-layout u38 at 1280x720@150).
            if (v.Phase == BoatPhase.Moored && TextWidth(_reqDone) > _reqDone.Size.X) _reqDone.Text = $"Loaded {v.Loaded} of {v.Want}, {secs} s left";
            _reqReward.Text = $"Pays {Money(t.BoatUnitPrice(v.Good))} a unit, more when filled";
            _reqDone.AddThemeColorOverride("font_color", UiKit.MutedNumber);
            _reqTintDirty = true;
        }
        float k = (float)Math.Clamp(v.PatienceMax > 0 ? v.Patience / v.PatienceMax : 0, 0, 1);
        SetGoalBar(v.Want > 0 ? Math.Min(v.Loaded, v.Want) / (float)v.Want : 0, k, _reqDone, UiKit.MutedNumber, v.Phase == BoatPhase.Moored);
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
