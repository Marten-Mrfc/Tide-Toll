using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 trades (build sheet part 2): the villager's card's trade. The title "Joss · Reaper" (name bold, trade muted); line 2
/// "Likes reaping · by the well lane" (the home dropped whole when it does not fit; a like whose trade is not open is not
/// said); the last line "Trade" and one cap of 40 x 32 per open trade, the Helper last: the current one darker paper with
/// an ink border of 2, the liked one with an ink corner fold, each with a one-line tooltip. A click sets the trade (that
/// villager is yours from then on). The map tags: "Joss, reaper" on hover; the first three villagers the town gives a
/// trade show their tag for 2 s. The one-time hint is the fifth first hint (TownHud.FirstMinute.cs).
/// </summary>
public partial class TownHud
{
    public const float TradeCapW = 40, TradeCapH = 32, TradeCapsX = 60, TradeCapsY = 112, TradeCapGap = 4;
    public const float AutoTagSeconds = 2f;
    public const int AutoTags = 3;
    private static readonly string[] LikesWords = { "", "Likes carrying water", "Likes reaping", "Likes milling" };
    private static readonly string[] TradeTitle = { "Helper", "Carrier", "Reaper", "Miller" };

    private Label _pTrade = null!, _pCapsLabel = null!;
    private readonly Button[] _caps = new Button[4];
    private readonly TradeMark[] _capFold = new TradeMark[4];
    private StyleBoxFlat _capCurrent = null!, _capRest = null!, _capHover = null!;
    private readonly int[] _capLook = new int[4];
    private int _capSig = int.MinValue, _titleSig = int.MinValue;

    /// <summary>The cap of a trade on the open card (tests click it), and whether it shows as current / liked.</summary>
    public Rect2 TradeCapRect(Trade t) => _caps[CapIndex(t)].Visible ? _caps[CapIndex(t)].GetGlobalRect() : new Rect2();
    public bool TradeCapCurrent(Trade t) => _capLook[CapIndex(t)] == 2;
    public bool TradeCapLiked(Trade t) => _capFold[CapIndex(t)].Visible;
    private static int CapIndex(Trade t) => t == Trade.None ? 3 : (int)t - 1;

    private void BuildPersonTrade(int pad, int inner)
    {
        float s = _s;
        _pTrade = UiKit.Fixed(s, UiKit.Body, UiKit.UiFont, UiKit.Muted);
        _pTrade.Name = "Trade"; _pTrade.Position = new Vector2(pad, UiKit.U(s, 8)); _pTrade.Size = new Vector2(inner, UiKit.U(s, 24));
        _person.AddChild(_pTrade);
        _pCapsLabel = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted);
        _pCapsLabel.Name = "TradeLabel"; _pCapsLabel.Text = "Trade";
        _pCapsLabel.Position = new Vector2(pad, UiKit.U(s, TradeCapsY)); _pCapsLabel.Size = new Vector2(UiKit.U(s, TradeCapsX - 16), UiKit.U(s, TradeCapH));
        _pCapsLabel.VerticalAlignment = VerticalAlignment.Center;
        _person.AddChild(_pCapsLabel);
        int r = UiKit.U(s, 4);
        _capCurrent = PillStyle(UiKit.PaperDarker, Math.Max(1, UiKit.U(s, 2)), UiKit.Ink, r);
        _capRest = PillStyle(UiKit.Paper, 1, UiKit.Muted, r);
        _capHover = PillStyle(UiKit.PaperLight, 1, UiKit.Ink, r);
        for (int k = 0; k < 4; k++)
        {
            var tr = TradeOrder[k];
            var b = new Button { Name = "Cap" + TradeTitle[(int)tr], Flat = true, FocusMode = Control.FocusModeEnum.None, Visible = false };
            foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) b.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
            b.Size = new Vector2(UiKit.U(s, TradeCapW), UiKit.U(s, TradeCapH));
            var face = new Panel { Name = "Face", MouseFilter = Control.MouseFilterEnum.Ignore, Size = b.Size };
            face.AddThemeStyleboxOverride("panel", _capRest);
            b.AddChild(face);
            var ic = UiKit.IconRect(s, IconOr(TradeIcons[(int)tr], "ui-coin"), 24);
            ic.MouseFilter = Control.MouseFilterEnum.Ignore;
            ic.Position = ((b.Size - new Vector2(UiKit.U(s, 24), UiKit.U(s, 24))) / 2f).Round();
            b.AddChild(ic);
            var fold = new TradeMark { Name = "Fold", Kind = TradeMark.Shape.Fold, Colour = UiKit.Ink, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            fold.Size = new Vector2(UiKit.U(s, 8), UiKit.U(s, 8));
            fold.Position = new Vector2(b.Size.X - fold.Size.X, 0);
            b.AddChild(fold);
            _capFold[k] = fold;
            b.Pressed += () => { if (SelectedVillager >= 0 && _personF.On) _drv.Town.SetVillagerTrade(SelectedVillager, tr); };
            _person.AddChild(b);
            _caps[k] = b;
            _capLook[k] = -1;
        }
        _capSig = int.MinValue; _titleSig = int.MinValue;
    }

    /// <summary>The title's trade, line 2 and the caps: made only when what they say changes.</summary>
    private void PersonTradeFrame(Town t, int id, VillagerView v)
    {
        int tsig = HashCode.Combine(id, v.Trade, v.Likes, _s);
        if (tsig != _titleSig)
        {
            _titleSig = tsig;
            string name = t.VillagerName(id);
            _pName.Text = name;
            float nw = UiKit.UiBold.GetStringSize(name, HorizontalAlignment.Left, -1, UiKit.Type(_s, UiKit.Body)).X;
            _pTrade.Text = " · " + TradeTitle[(int)v.Trade];
            _pTrade.Position = new Vector2(_pName.Position.X + MathF.Ceiling(nw), _pTrade.Position.Y);
            _pTrade.Size = new Vector2(Math.Max(0, _pName.Size.X - MathF.Ceiling(nw)), _pTrade.Size.Y);
            _pHome.Text = LikeAndHome(t, v, _pHome.Size.X, UiKit.Type(_s, UiKit.Caption));
        }
        int csig = HashCode.Combine(v.Trade, v.Likes, t.TradeOpen(Trade.Grain), t.TradeOpen(Trade.Flour), id);
        var mouse = _root.GetGlobalMousePosition();
        if (csig != _capSig)
        {
            _capSig = csig;
            float x = UiKit.U(_s, TradeCapsX) + _pCapsLabel.Position.X - UiKit.U(_s, 12);
            for (int k = 0; k < 4; k++)
            {
                var tr = TradeOrder[k];
                bool open = tr == Trade.None || t.TradeOpen(tr);
                _caps[k].Visible = open;
                if (!open) continue;
                _caps[k].Position = new Vector2(x, UiKit.U(_s, TradeCapsY));
                x += UiKit.U(_s, TradeCapW + TradeCapGap);
                _capFold[k].Visible = tr != Trade.None && v.Likes == tr;
                _capLook[k] = -1;
            }
        }
        for (int k = 0; k < 4; k++)
        {
            if (!_caps[k].Visible) continue;
            int look = TradeOrder[k] == v.Trade ? 2 : _caps[k].GetGlobalRect().HasPoint(mouse) ? 1 : 0;
            if (look == _capLook[k]) continue;
            _capLook[k] = look;
            _caps[k].GetChild<Panel>(0).AddThemeStyleboxOverride("panel", look == 2 ? _capCurrent : look == 1 ? _capHover : _capRest);
        }
    }

    /// <summary>"Likes reaping · by the well lane"; the home dropped whole when it does not fit; "Lives by the well lane"
    /// while the like's trade is not open.</summary>
    public static string LikeAndHome(Town t, VillagerView v, float room, int px)
    {
        string home = HomeWords(t, v);
        if (v.Likes == Trade.None) return home;
        string like = LikesWords[(int)v.Likes];
        string both = like + " · " + (home.StartsWith("Lives ") ? home.Substring(6) : home);
        return UiKit.UiFont.GetStringSize(both, HorizontalAlignment.Left, -1, px).X <= room ? both : like;
    }

    // ---- Tooltips of the caps ----

    private Control? PersonCapOwner(Vector2 p)
    {
        if (!_person.Visible || _person.Modulate.A < 0.5f || SelectedVillager < 0) return null;
        foreach (var c in _caps) if (c.Visible && c.GetGlobalRect().HasPoint(p)) return c;
        return null;
    }

    private string? PersonCapTip(Control owner)
    {
        for (int k = 0; k < 4; k++)
        {
            if (owner != _caps[k] || SelectedVillager < 0) continue;
            var t = _drv.Town;
            var v = t.Villager(SelectedVillager);
            var tr = TradeOrder[k];
            string name = TradeTitle[(int)tr];
            if (tr == Trade.None) return v.Trade == tr ? "Helper: no trade, works where it pays best" : "Helper: no trade, works where it pays best. Click to make it one";
            int likes = (int)Math.Round(TownRules.TradeLikes * 100);
            string s = name + (v.Likes == tr ? (likes > 0 ? $": likes this work, +{likes}%" : ": likes this work") : "");
            return v.Trade == tr ? s + " (now)" : s;
        }
        return null;
    }

    // ---- Map tags: the first three villagers the town gives a trade ----

    private readonly Label?[] _autoTag = new Label?[AutoTags];
    private readonly int[] _autoWho = new int[AutoTags];
    private readonly float[] _autoAge = new float[AutoTags], _autoA = new float[AutoTags];
    private int _autoGiven;

    private void BuildAutoTags()
    {
        for (int k = 0; k < AutoTags; k++)
        {
            _autoTag[k] = Tag("TradeTag" + k, UiKit.Caption);
            _autoAge[k] = AutoTagSeconds; _autoA[k] = 0f; _autoWho[k] = -1;
        }
    }

    private void TradesEvent(in TownEvent e)
    {
        if (e.Kind != TownEventKind.TradeChanged || e.Level != 0 || e.Index == 0 || _autoGiven >= AutoTags) return;
        int k = _autoGiven++;
        var l = _autoTag[k];
        if (l == null) return;
        _autoWho[k] = e.Villager; _autoAge[k] = 0f;
        l.Text = _drv.Town.VillagerName(e.Villager) + ", " + TradeOne[e.Index];
        l.ResetSize();
    }

    /// <summary>A new town: the first three tags again.</summary>
    private void ResetTradeTags() { _autoGiven = 0; for (int k = 0; k < AutoTags; k++) { _autoAge[k] = AutoTagSeconds; _autoWho[k] = -1; } }

    private void AutoTagsFrame(float dt)
    {
        var v = _main.Villagers;
        for (int k = 0; k < AutoTags; k++)
        {
            var l = _autoTag[k];
            if (l == null) continue;
            _autoAge[k] += dt;
            bool want = _autoWho[k] >= 0 && _autoWho[k] < _drv.Town.VillagerCount && _autoAge[k] < AutoTagSeconds && PickingOnForCards && _autoWho[k] != _tagFor;
            _autoA[k] = Math.Clamp(_autoA[k] + (want ? dt : -dt) / 0.12f, 0f, 1f);
            bool show = _autoA[k] > 0.001f && _autoWho[k] >= 0;
            if (l.Visible != show) l.Visible = show;
            if (!show) continue;
            var head = _main.Rig.LocalToScreen(v.FeetLocal(_autoWho[k]) + new Vector3(0, v.FigureHeight, 0));
            l.Position = (head - new Vector2(l.Size.X / 2f, l.Size.Y + UiKit.Px(_s, 6))).Round();
            l.Modulate = new Color(1, 1, 1, _autoA[k]);
        }
    }

    /// <summary>Opacity of the auto tags now (tests: they fade, never switch in one frame).</summary>
    public float AutoTagAlpha(int k) => _autoA[k];
    public int AutoTagsGiven => _autoGiven;
}
