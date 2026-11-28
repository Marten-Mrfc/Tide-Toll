using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 trades (docs/plans/n7/trades-build-sheet.md part 2): the Trades card in the Person zone, 156 high, shown from the
/// first field on while no villager's card is open (the two cards crossfade in the one rectangle over 0.2 s). Heading
/// "Trades" with the quiet switch "Town fills trades" at the right; one line of 28 per open trade (Carriers, Reapers,
/// Millers) and the Helpers last: icon 24, name, the stepper (minus, the count in a fixed cell, plus; pills 32 x 24
/// drawn, hit areas 40 x 28). A count the town decides is muted and regular, one you set is ink and bold. The Helpers'
/// line has no stepper: the caption "work where it pays best" stands in its place. Hovering a line rings its members on
/// the map. A click acts on what was shown 0.3 s ago (as the shop's rows do). Text only when what it says changes.
/// Pills and tooltips: TownHud.TradesParts.cs; the villager's Trade caps: TownHud.PeopleTrade.cs.
/// </summary>
public partial class TownHud
{
    public const float TradesCardH = 156, TradeLineH = 28, TradeLineTop = 40, TradesFadeSeconds = 0.2f;
    public const float StepPillW = 32, StepPillH = 24, StepHitW = 40, StepHitH = 28, CountCellW = 28;
    /// <summary>A click acts on the state shown this long ago when the state has just changed.</summary>
    public const float TradesClickLag = 0.3f;
    /// <summary>The lines in display order: the open trades, the Helpers last.</summary>
    public static readonly Trade[] TradeOrder = { Trade.Water, Trade.Grain, Trade.Flour, Trade.None };
    // By (int)Trade. The second age's three (Shepherds, Weavers, Porters) are named here so nothing indexes past the end;
    // their lines, icons and figures come with the second age's interface.
    public static readonly string[] TradeNames = { "Helpers", "Carriers", "Reapers", "Millers", "Shepherds", "Weavers", "Porters" };
    public static readonly string[] TradeFullNames = { "Helpers", "Water carriers", "Reapers", "Millers", "Shepherds", "Weavers", "Porters" };
    public static readonly string[] TradeOne = { "helper", "carrier", "reaper", "miller", "shepherd", "weaver", "porter" };
    public static readonly string[] TradeIcons = { "trade-helper", "trade-water", "trade-grain", "trade-flour", "trade-wool", "trade-cloth", "trade-boat" };
    public const string HelpersCaption = "work where it pays best";

    private Panel _trades = null!;
    private Label _tHead = null!, _tSwitchText = null!;
    private Button _tSwitch = null!;
    private Panel _tSwitchBox = null!;
    private TradeMark _tCheck = null!;
    private readonly TradeLine[] _tLines = new TradeLine[4];
    private readonly MessageFader _tradesF = new(TradesFadeSeconds);
    private float _checkA = 1f;
    private int _tradesSig = int.MinValue;
    private bool _tradesBuilt;

    /// <summary>Opacity of the Trades card now and the line hovered (-1 none; else its Trade), for tests.</summary>
    public float TradesCardAlpha => _trades != null && _trades.Visible ? _trades.Modulate.A : 0f;
    public float PersonCardAlpha => _person != null && _person.Visible ? _person.Modulate.A : 0f;
    public Rect2 TradesCardRect => _trades.Visible ? _trades.GetGlobalRect() : new Rect2();
    public int TradeHovered { get; private set; } = -1;
    /// <summary>A line's parts (tests point at them as a player would): the hit areas, the drawn pills, the count.</summary>
    public Rect2 TradeLineRect(Trade t) => LineOf(t) is { } l && l.Row.Visible ? l.Row.GetGlobalRect() : new Rect2();
    public Rect2 TradeMinusHit(Trade t) => LineOf(t) is { } l && l.Minus.Hit.Visible ? l.Minus.Hit.GetGlobalRect() : new Rect2();
    public Rect2 TradePlusHit(Trade t) => LineOf(t) is { } l && l.Plus.Hit.Visible ? l.Plus.Hit.GetGlobalRect() : new Rect2();
    public Rect2 TradeMinusPill(Trade t) => LineOf(t) is { } l ? l.Minus.Face.GetGlobalRect() : new Rect2();
    public Rect2 TradePlusPill(Trade t) => LineOf(t) is { } l ? l.Plus.Face.GetGlobalRect() : new Rect2();
    public string TradeCountText(Trade t) => LineOf(t) is { } l ? l.Count.Text : "";
    public bool TradeCountBold(Trade t) => LineOf(t) is { } l && l.Bold;
    public bool TradeMinusEnabled(Trade t) => LineOf(t) is { } l && l.Minus.Enabled;
    public bool TradePlusEnabled(Trade t) => LineOf(t) is { } l && l.Plus.Enabled;
    public Rect2 TradesSwitchRect => _tSwitch.GetGlobalRect();
    public bool TradesSwitchOn => _tCheck.Visible && _checkA > 0.5f;
    /// <summary>The Helpers' caption while it shows ("" when dropped for want of room).</summary>
    public string HelpersCaptionShown => _tLines[3] != null && _tLines[3].Caption.Visible ? _tLines[3].Caption.Text : "";

    private TradeLine? LineOf(Trade t)
    {
        foreach (var l in _tLines) if (l != null && l.Trade == t) return l;
        return null;
    }

    private void BuildTrades()
    {
        var z = Zones;
        float s = _s;
        int pad = UiKit.U(s, 12), w = (int)z.Person.Size.X;
        _trades = new Panel { Name = "Trades", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _trades.AddThemeStyleboxOverride("panel", UiKit.CardBox(s));
        _trades.Position = z.Person.Position;
        _trades.Size = new Vector2(w, UiKit.U(s, TradesCardH));
        _root.AddChild(_trades);
        _tHead = UiKit.Fixed(s, UiKit.Title, UiKit.Serif, UiKit.Ink);
        _tHead.Name = "Heading"; _tHead.Text = "Trades";
        _tHead.Position = new Vector2(pad, UiKit.U(s, 6)); _tHead.Size = new Vector2(UiKit.U(s, 100), UiKit.U(s, 28));
        _trades.AddChild(_tHead);
        // The switch: a small paper box with an ink check when on, its caption before it, at the right of the heading.
        _tSwitch = new Button { Name = "Switch", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Stop };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) _tSwitch.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
        _tSwitchText = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted, HorizontalAlignment.Right);
        _tSwitchText.Name = "Caption"; _tSwitchText.Text = "Town fills trades";
        _tSwitchText.MouseFilter = Control.MouseFilterEnum.Ignore;
        int box = UiKit.U(s, 16), sw = (int)Math.Ceiling(UiKit.UiFont.GetStringSize(_tSwitchText.Text, HorizontalAlignment.Left, -1, UiKit.Type(s, UiKit.Caption)).X) + 2;
        int swW = sw + UiKit.U(s, 6) + box + UiKit.U(s, 8), swH = UiKit.U(s, 28);
        _tSwitch.Position = new Vector2(w - pad - swW + UiKit.U(s, 4), UiKit.U(s, 6));
        _tSwitch.Size = new Vector2(swW, swH);
        _tSwitchText.Position = new Vector2(UiKit.U(s, 4), 0); _tSwitchText.Size = new Vector2(sw, swH);
        _tSwitchText.VerticalAlignment = VerticalAlignment.Center;
        _tSwitch.AddChild(_tSwitchText);
        _tSwitchBox = new Panel { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _tSwitchBox.AddThemeStyleboxOverride("panel", PillStyle(UiKit.PaperLight, 1, UiKit.Ink, UiKit.U(s, 3)));
        _tSwitchBox.Position = new Vector2(swW - UiKit.U(s, 4) - box, (swH - box) / 2f);
        _tSwitchBox.Size = new Vector2(box, box);
        _tSwitch.AddChild(_tSwitchBox);
        _tCheck = new TradeMark { Name = "Check", Kind = TradeMark.Shape.Check, Colour = UiKit.Ink, Width = Math.Max(1.5f, UiKit.U(s, 2)), MouseFilter = Control.MouseFilterEnum.Ignore };
        _tCheck.Size = _tSwitchBox.Size; _tSwitchBox.AddChild(_tCheck);
        _tSwitch.Pressed += () => { if (_tradesF.On) _drv.Town.SetTownFillsTrades(!_drv.Town.TownFillsTrades); };
        _trades.AddChild(_tSwitch);
        for (int k = 0; k < 4; k++)
        {
            var l = new TradeLine(this, TradeOrder[k], s, w, pad);
            _tLines[k] = l;
            _trades.AddChild(l.Row);
        }
        _tradesF.Set(_drv.Town.TradesShown && SelectedVillager < 0);
        _checkA = _drv.Town.TownFillsTrades ? 1f : 0f;
        _tradesSig = int.MinValue;
        _tradesBuilt = true;
        TradesLayout(_drv.Town, true);
    }

    /// <summary>The lines' places: open trades from the top, the Helpers last; a line that opens fades in and the Helpers
    /// slide down to make room (over 0.2 s, never in one frame).</summary>
    private void TradesLayout(Town t, bool snap, float dt = 0f)
    {
        int slot = 0;
        foreach (var l in _tLines)
        {
            bool open = l.Trade == Trade.None || t.TradeOpen(l.Trade);
            l.Open = open;
            if (!open) continue;
            float want = UiKit.U(_s, TradeLineTop + TradeLineH * slot++);
            if (snap || l.Y < 0) { l.Y = want; l.A = 1f; }
            float step = UiKit.U(_s, TradeLineH) * dt / TradesFadeSeconds;
            l.Y = Math.Abs(want - l.Y) <= step ? want : l.Y + Math.Sign(want - l.Y) * step;
        }
        foreach (var l in _tLines)
        {
            l.A = l.Open ? Math.Min(1f, l.A + dt / TradesFadeSeconds) : 0f;
            if (snap && l.Open) l.A = 1f;
            bool vis = l.Open && l.A > 0.001f;
            if (l.Row.Visible != vis) l.Row.Visible = vis;
            if (!vis) continue;
            if (l.Row.Position.Y != MathF.Round(l.Y)) l.Row.Position = new Vector2(l.Row.Position.X, MathF.Round(l.Y));
            l.Row.Modulate = new Color(1, 1, 1, l.A);
        }
    }

    private void TradesFrame(float dt)
    {
        if (!_tradesBuilt) return;
        var t = _drv.Town;
        t.SetOpenCard(SelectedVillager);
        _tradesF.Want(t.TradesShown && SelectedVillager < 0);
        _tradesF.Step(dt);
        float a = _tradesF.Value * _fullA;
        bool show = a > 0.001f;
        if (_trades.Visible != show) _trades.Visible = show;
        // Rings of the hovered line's members (also when the card fades out, they fade with it).
        int hov = -1;
        if (show && _tradesF.On && PickingOnForCards)
        {
            var p = _root.GetGlobalMousePosition();
            foreach (var l in _tLines) if (l.Row.Visible && l.A > 0.5f && l.Row.GetGlobalRect().HasPoint(p)) hov = (int)l.Trade;
        }
        TradeHovered = hov;
        _main.Villagers.HoverTrade = hov;
        if (!show) return;
        _trades.Modulate = new Color(1, 1, 1, a);
        TradesLayout(t, false, dt);
        bool on = t.TownFillsTrades;
        _checkA = Math.Clamp(_checkA + (on ? dt : -dt) / 0.15f, 0f, 1f);
        bool cv = _checkA > 0.001f;
        if (_tCheck.Visible != cv) _tCheck.Visible = cv;
        _tCheck.Modulate = new Color(1, 1, 1, _checkA);
        var mouse = _root.GetGlobalMousePosition();
        foreach (var l in _tLines) if (l.Row.Visible) l.Frame(t, dt, hov == (int)l.Trade, mouse);
    }

    /// <summary>The cards take the pointer while the open map does (not over a page, not paused).</summary>
    private bool PickingOnForCards => !(_main.Menu != null && _main.Menu.IsPaused) && _fullA > 0.5f && !AchievementsOpen && !AgePageOpen && _fade > 0.5f;

    private bool OverTrades(Vector2 p) => Over(_trades, p);
}
