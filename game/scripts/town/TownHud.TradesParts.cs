using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 trades: the parts of the Trades card (TownHud.Trades.cs): one line (band, icon, name, count, stepper or caption),
/// the stepper's pills in the shop pill's states (rest, hover, pressed, disabled) with hit areas larger than the drawn
/// pill, the small drawn marks (the switch's check, the liked cap's corner fold), and the card's tooltips in the shop
/// tip's form (a sentence, then facts), their numbers frozen while a tip shows.
/// </summary>
public partial class TownHud
{
    private static StyleBoxFlat PillStyle(Color bg, int border, Color edge, int corner)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true };
        b.SetBorderWidthAll(border);
        b.SetCornerRadiusAll(corner);
        b.SetMeta("ui_card", "Pill");
        return b;
    }

    /// <summary>One pill of the stepper: a hit area of 40 x 28 units around a drawn pill of 32 x 24.</summary>
    private sealed class StepPill
    {
        public readonly Button Hit;
        public readonly Panel Face;
        private readonly ColorRect _barH, _barV;
        private readonly StyleBoxFlat[] _looks;
        private int _look = -1;
        public bool Enabled = true, Before = true;
        public float Since = 99f;
        public Action? Act;

        public StepPill(float s, bool plus, StyleBoxFlat[] looks)
        {
            _looks = looks;
            Hit = new Button { Name = plus ? "Plus" : "Minus", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Stop };
            foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) Hit.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
            Hit.Size = new Vector2(UiKit.U(s, StepHitW), UiKit.U(s, StepHitH));
            Face = new Panel { Name = "Pill", MouseFilter = Control.MouseFilterEnum.Ignore };
            Face.Size = new Vector2(UiKit.U(s, StepPillW), UiKit.U(s, StepPillH));
            Face.Position = ((Hit.Size - Face.Size) / 2f).Round();
            Hit.AddChild(Face);
            float bl = UiKit.U(s, 10), bt = Math.Max(1, UiKit.U(s, 2));
            _barH = new ColorRect { Name = "Bar", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(bl, bt) };
            _barH.Position = ((Face.Size - _barH.Size) / 2f).Round();
            _barV = new ColorRect { Name = "BarV", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(bt, bl), Visible = plus };
            _barV.Position = ((Face.Size - _barV.Size) / 2f).Round();
            Face.AddChild(_barH); Face.AddChild(_barV);
            // A click acts on what was shown TradesClickLag ago when the state has just changed (as the shop's rows do).
            Hit.Pressed += () => { if (Since >= TradesClickLag ? Enabled : Before) Act?.Invoke(); };
        }

        public void SetEnabled(bool on)
        {
            if (on == Enabled) return;
            Before = Enabled; Enabled = on; Since = 0f;
        }

        /// <summary>The look: 0 rest, 1 hover, 2 pressed, 3 disabled (set only when it changes).</summary>
        public void Frame(float dt)
        {
            Since += dt;
            var m = Hit.GetDrawMode();
            int look = !Enabled ? 3 : m is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed ? 2 : m == BaseButton.DrawMode.Hover ? 1 : 0;
            if (look == _look) return;
            _look = look;
            Face.AddThemeStyleboxOverride("panel", _looks[look]);
            var c = look == 3 ? UiKit.DisabledInk : UiKit.Ink;
            _barH.Color = c; _barV.Color = c;
            // Pressed: the content one unit down.
            float dy = look == 2 ? 1f : 0f;
            _barH.Position = new Vector2(_barH.Position.X, MathF.Round((Face.Size.Y - _barH.Size.Y) / 2f) + dy);
            _barV.Position = new Vector2(_barV.Position.X, MathF.Round((Face.Size.Y - _barV.Size.Y) / 2f) + dy);
        }
    }

    /// <summary>One line of the Trades card.</summary>
    private sealed class TradeLine
    {
        public readonly Trade Trade;
        public readonly Button Row;
        public readonly Panel Band;
        public readonly Label Name, Count, Caption;
        public readonly StepPill Minus, Plus;
        public bool Open, Bold;
        public float Y = -1, A;
        private readonly TownHud _hud;
        private int _shown = -1, _next = -1;
        private bool _byPlayer, _boldNext;
        private float _countT = -1f, _bandA;
        private readonly float _s;

        public TradeLine(TownHud hud, Trade t, float s, int w, int pad)
        {
            _hud = hud; Trade = t; _s = s;
            int inset = UiKit.U(s, 4), rw = w - 2 * inset, h = UiKit.U(s, TradeLineH);
            Row = new Button { Name = "Line" + TradeNames[(int)t], Flat = true, FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Pass, Visible = false };
            foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) Row.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
            Row.Position = new Vector2(inset, 0); Row.Size = new Vector2(rw, h);
            Band = new Panel { Name = "Band", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, Size = Row.Size };
            var bb = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, 0.09f) };
            bb.SetCornerRadiusAll(UiKit.U(s, 4));
            bb.SetMeta("ui_card", "RowBand");
            Band.AddThemeStyleboxOverride("panel", bb);
            Row.AddChild(Band);
            int icon = UiKit.U(s, 24);
            var ic = UiKit.IconRect(s, IconOr(TradeIcons[(int)t], "ui-coin"), 24);
            ic.Name = "Icon"; ic.Position = new Vector2(pad - inset, (h - icon) / 2f); ic.MouseFilter = Control.MouseFilterEnum.Ignore;
            Row.AddChild(ic);
            int nameX = UiKit.U(s, 40) - inset;
            Name = UiKit.Fixed(s, UiKit.Body, UiKit.UiFont, UiKit.Ink);
            Name.Name = "Name"; Name.Text = TradeNames[(int)t]; Name.MouseFilter = Control.MouseFilterEnum.Ignore;
            Name.Position = new Vector2(nameX, 0); Name.Size = new Vector2(UiKit.U(s, 120), h); Name.VerticalAlignment = VerticalAlignment.Center;
            Row.AddChild(Name);
            var looks = hud.PillLooks(s);
            int pw = UiKit.U(s, StepPillW), gap = UiKit.U(s, 2), cell = UiKit.U(s, CountCellW), hitPad = (UiKit.U(s, StepHitW) - pw) / 2;
            float plusFace = rw - (pad - inset) - pw, cellX = plusFace - gap - cell, minusFace = cellX - gap - pw;
            Plus = new StepPill(s, true, looks); Minus = new StepPill(s, false, looks);
            Plus.Hit.Position = new Vector2(plusFace - hitPad, 0); Minus.Hit.Position = new Vector2(minusFace - hitPad, 0);
            Count = UiKit.Fixed(s, UiKit.Body, UiKit.TabRegular, UiKit.MutedNumber, HorizontalAlignment.Right);
            Count.Name = "Count"; Count.MouseFilter = Control.MouseFilterEnum.Ignore;
            // Right-aligned in the cell, 4 units clear of the plus pill (two bold figures stand off it).
            Count.Position = new Vector2(cellX, 0); Count.Size = new Vector2(cell - UiKit.U(s, 4), h); Count.VerticalAlignment = VerticalAlignment.Center;
            Row.AddChild(Count);
            Caption = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted, HorizontalAlignment.Right);
            Caption.Name = "Caption"; Caption.MouseFilter = Control.MouseFilterEnum.Ignore; Caption.VerticalAlignment = VerticalAlignment.Center;
            if (t == Trade.None)
            {
                // The Helpers: no stepper; the caption in its place, dropped whole when it does not fit beside the name.
                Caption.Text = HelpersCaption;
                float nameEnd = nameX + UiKit.UiFont.GetStringSize(Name.Text, HorizontalAlignment.Left, -1, UiKit.Type(s, UiKit.Body)).X + UiKit.U(s, 8);
                float cw = UiKit.UiFont.GetStringSize(HelpersCaption, HorizontalAlignment.Left, -1, UiKit.Type(s, UiKit.Caption)).X + 2;
                float right = cellX - UiKit.U(s, 6);
                Caption.Position = new Vector2(Math.Max(nameEnd, right - cw), 0); Caption.Size = new Vector2(right - Caption.Position.X, h);
                Caption.Visible = right - nameEnd >= cw;
                Caption.ClipText = true;
                // No stepper on this line: the pills stay in the tree, hidden (a node out of the tree would leak).
                Minus.Hit.Visible = false; Plus.Hit.Visible = false;
                Row.AddChild(Minus.Hit); Row.AddChild(Plus.Hit);
            }
            else
            {
                Caption.Visible = false;
                Row.AddChild(Minus.Hit); Row.AddChild(Plus.Hit);
                Minus.Act = () => { var tv = hud._drv.Town.TradeInfo(Trade); hud._drv.Town.SetTradeCount(Trade, tv.Members - 1); };
                Plus.Act = () => { var tv = hud._drv.Town.TradeInfo(Trade); hud._drv.Town.SetTradeCount(Trade, tv.Members + 1); };
            }
            Row.AddChild(Caption);
        }

        public void Frame(Town t, float dt, bool hovered, Vector2 mouse)
        {
            var tv = t.TradeInfo(Trade);
            // The count: its digits fade out and in over 0.2 s when it changes; ink and bold when you set it.
            if (_next != tv.Members || _boldNext != tv.ByPlayer)
            {
                _next = tv.Members; _boldNext = tv.ByPlayer;
                if (_shown < 0) { Show(); _countT = -1f; }
                else if (_countT < 0f) _countT = 0f;
            }
            if (_countT >= 0f)
            {
                _countT += dt;
                float half = TradesFadeSeconds / 2f;
                if (_countT >= half && (_shown != _next || _byPlayer != _boldNext)) Show();
                float u = _countT >= TradesFadeSeconds ? 1f : Math.Abs(_countT - half) / half;
                Count.Modulate = new Color(1, 1, 1, u);
                if (_countT >= TradesFadeSeconds) _countT = -1f;
            }
            if (Trade != Trade.None)
            {
                Minus.SetEnabled(tv.Members > 1 && t.CanLowerTrade(Trade));
                Plus.SetEnabled(t.CanRaiseTrade(Trade));
                Minus.Frame(dt); Plus.Frame(dt);
            }
            _bandA = Math.Clamp(_bandA + (hovered ? dt : -dt) / 0.1f, 0f, 1f);
            bool bv = _bandA > 0.001f;
            if (Band.Visible != bv) Band.Visible = bv;
            if (bv) Band.Modulate = new Color(1, 1, 1, _bandA);
        }

        private void Show()
        {
            _shown = _next; _byPlayer = _boldNext; Bold = _byPlayer;
            Count.Text = _shown.ToString();
            Count.AddThemeFontOverride("font", _byPlayer ? UiKit.TabBold : UiKit.TabRegular);
            Count.AddThemeColorOverride("font_color", _byPlayer ? UiKit.Ink : UiKit.Muted);
        }
    }

    private StyleBoxFlat[]? _pillLooks;
    private float _pillLooksFor = -1f;
    /// <summary>The stepper's pill looks (rest, hover, pressed, disabled), as the shop's pills: made once per build.</summary>
    private StyleBoxFlat[] PillLooks(float s)
    {
        if (_pillLooks != null && _pillLooksFor == s) return _pillLooks;
        int r = UiKit.U(s, StepPillH / 2f);
        _pillLooksFor = s;
        return _pillLooks = new[]
        {
            PillStyle(UiKit.Paper, 1, UiKit.Muted, r), PillStyle(UiKit.PaperLight, 1, UiKit.Ink, r),
            PillStyle(UiKit.PaperDarker, 1, UiKit.Ink, r), PillStyle(UiKit.Paper, 1, new Color(UiKit.Ink, 0.3f), r),
        };
    }

    // ---- Tooltips ----

    private Control? _tradeTipFor;
    private string _tradeTipText = "";

    /// <summary>The Trades card's tooltip owner under the pointer: a disabled pill (it says why), the switch, a line.</summary>
    private Control? TradesTipOwner(Vector2 p)
    {
        if (_trades == null || !_trades.Visible || _trades.Modulate.A < 0.5f || !_trades.GetGlobalRect().HasPoint(p)) return null;
        if (Hit(_tSwitch, p)) return _tSwitch;
        foreach (var l in _tLines)
        {
            if (!l.Row.Visible || !l.Row.GetGlobalRect().HasPoint(p)) continue;
            if (l.Trade != Trade.None && !l.Minus.Enabled && Hit(l.Minus.Hit, p)) return l.Minus.Hit;
            if (l.Trade != Trade.None && !l.Plus.Enabled && Hit(l.Plus.Hit, p)) return l.Plus.Hit;
            return l.Row;
        }
        return null;
    }

    /// <summary>The tip's text, made when the pointer comes to the owner and kept (numbers frozen) while it shows.</summary>
    private string? TradesTipText(Control owner)
    {
        if (owner == _tradeTipFor) return _tradeTipText;
        string? s = null;
        var t = _drv.Town;
        if (owner == _tSwitch)
            s = t.TownFillsTrades
                ? "Town fills trades: the town gives every villager a trade and now and then moves one to where it pays best. It never moves a count or a villager you set. Click to leave the trades as they are."
                : "The town leaves the trades as they are. Click to let it fill them again: every count is the town's again (villagers you set one by one stay set).";
        foreach (var l in _tLines)
        {
            if (owner == l.Minus.Hit)
                s = t.TradeInfo(l.Trade).Members <= 0 ? $"No {TradeNames[(int)l.Trade].ToLowerInvariant()} to take away." : $"{TradeNames[(int)l.Trade]} keep one member while the trade is open.";
            else if (owner == l.Plus.Hit)
                s = "Nobody to take: there are no Helpers, and every other trade is down to one member or set by you.";
            else if (owner == l.Row) s = TradeLineTip(t, l.Trade);
        }
        if (s == null) return null;
        _tradeTipFor = owner; _tradeTipText = s;
        return s;
    }

    private static readonly string[] TradeSentences =
    {
        "Helpers have no trade: they take whatever work pays best.",
        "Water carriers draw water at the well and carry it home.",
        "Reapers bring in the grain.",
        "Millers grind the grain into flour.",
        "Shepherds bring in the wool.",
        "Weavers make the cloth.",
        "Porters carry grain and flour to the boats.",
    };

    private static string TradeLineTip(Town t, Trade tr)
    {
        var tv = t.TradeInfo(tr);
        var sb = new System.Text.StringBuilder(TradeSentences[(int)tr]);
        // Fix round: worded so it cannot be read as members with nowhere to work (most members are on the road).
        sb.Append("\nMembers ").Append(tv.Members);
        if (tr != Trade.None) sb.Append("\nWork places ").Append(tv.Places).Append(" (the others are on the road)");
        if (tv.Waiting > 0) sb.Append("\nWaiting now ").Append(tv.Waiting);
        if (tr != Trade.None && tv.Away > 0) sb.Append("\nAway now ").Append(tv.Away).Append(" (at another trade's work)");
        if (tr != Trade.None && (tv.WorkBonus > 0 || tv.WorthBonus > 0))
            sb.Append("\nBonus: work +").Append(Math.Round(tv.WorkBonus * 100)).Append("%, goods +").Append(Math.Round(tv.WorthBonus * 100)).Append('%');
        if (tv.ByPlayer) sb.Append("\nYou set this count. The town leaves it alone.");
        return sb.ToString();
    }
}
