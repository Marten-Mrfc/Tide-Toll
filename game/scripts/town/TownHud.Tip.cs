using System;
using System.Text;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, section 6): one tooltip control for the play interface (not the engine's): shown after
/// 0.25 s of hover, fading in over 0.12 s, placed beside its owner by <see cref="HudZones.TipRect"/> (left of a shop
/// row, right of the ledger and the timers, above a navigation button), inside the window, off its owner and off a
/// showing message. This round: shop rows, group headings, the bell, the tide, navigation and the purse. Its text is
/// made when the pointer comes to an owner (and twice a second while it stays), never per frame otherwise.
/// </summary>
public partial class TownHud
{
    public const float TipDelay = 0.25f, TipFadeIn = 0.12f, TipRefresh = 0.5f;
    private PanelContainer _tip = null!;
    private Label _tipLabel = null!;
    private Control? _tipOwner;
    private float _tipAge, _tipRefresh;
    private readonly StringBuilder _tipSb = new();

    /// <summary>The tooltip's owner, text and rectangle while it shows (tests).</summary>
    public Control? TipOwner => _tip.Visible ? _tipOwner : null;
    public string TipText => _tip.Visible ? _tipLabel.Text : "";
    public Rect2 TipShownRect => _tip.Visible ? _tip.GetGlobalRect() : new Rect2();

    private void BuildTip()
    {
        _tip = new PanelContainer { Name = "Tooltip", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _tip.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _tip.AddChild(holder);
        _tipLabel = UiKit.Fixed(_s, UiKit.Caption);
        _tipLabel.Name = "Text";
        _tipLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tipLabel.VerticalAlignment = VerticalAlignment.Top;
        _tipLabel.ClipText = false;
        holder.AddChild(_tipLabel);
        _root.AddChild(_tip);
        _tipOwner = null;
    }

    /// <summary>The owner under the pointer, and the side its tip opens on.</summary>
    private Control? TipOwnerAt(Vector2 p, out HudZones.TipSide side)
    {
        side = HudZones.TipSide.Left;
        if (_main.Menu != null && _main.Menu.IsPaused) return null;
        if (_shop.IsVisibleInTree() && _shop.Modulate.A > 0.5f && _shop.GetGlobalRect().HasPoint(p))
        {
            foreach (var r in _rowOrder) if (r.Button.Visible && r.Alpha > 0.5f && r.Button.GetGlobalRect().HasPoint(p)) return r.Button;
            for (int g = 0; g < 5; g++) if (_groupHead[g].Visible && _groupHead[g].GetGlobalRect().HasPoint(p)) return _groupHead[g];
            return null;
        }
        side = HudZones.TipSide.Right;
        if (Hit(_bell, p)) return _bell;
        if (Hit(_tide, p)) return _tide;
        if (_ledgerPanel.Modulate.A > 0.5f && Hit(_purse, p)) return _purse;
        side = HudZones.TipSide.Above;
        if (Hit(_achMark, p)) return _achMark;
        if (Hit(_agePageMark, p)) return _agePageMark;
        if (Hit(_home, p)) return _home;
        if (Hit(PauseMark, p)) return PauseMark;
        return null;
    }

    private static bool Hit(Control c, Vector2 p) => c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(p);

    private void TipFrame()
    {
        float dt = (float)GetProcessDeltaTime();
        var p = _root.GetGlobalMousePosition();
        var owner = TipOwnerAt(p, out var side);
        if (owner != _tipOwner) { _tipOwner = owner; _tipAge = 0; _tip.Visible = false; }
        if (owner == null) return;
        _tipAge += dt;
        if (_tipAge < TipDelay) return;
        _tipRefresh -= dt;
        if (!_tip.Visible || _tipRefresh <= 0)
        {
            _tipRefresh = TipRefresh;
            string text = TipTextFor(owner);
            if (text.Length == 0) { _tip.Visible = false; return; }
            if (_tipLabel.Text != text) _tipLabel.Text = text;
            float w = UiKit.U(_s, HudZones.TipW), pad = UiKit.U(_s, 12);
            var f = _tipLabel.GetThemeFont("font");
            float h = f.GetMultilineStringSize(text, HorizontalAlignment.Left, w - 2 * pad, _tipLabel.GetThemeFontSize("font_size")).Y;
            _tipLabel.Position = new Vector2(pad, pad);
            _tipLabel.Size = new Vector2(w - 2 * pad, h + 2);
            var size = new Vector2(w, MathF.Ceiling(h) + 2 * pad + 2);
            var avoid = _choice.Visible ? _choice.GetGlobalRect() : _banner.Visible ? _banner.GetGlobalRect() : new Rect2();
            var r = Zones.TipRect(owner.GetGlobalRect(), size, side, avoid);
            _tip.Position = r.Position;
            _tip.Size = r.Size;
            _tip.Visible = true;
        }
        _tip.Modulate = new Color(1, 1, 1, Math.Clamp((_tipAge - TipDelay) / TipFadeIn, 0, 1));
    }

    private string TipTextFor(Control owner)
    {
        var t = _drv.Town;
        var sb = _tipSb.Clear();
        for (int i = 0; i < _rowOrder.Count; i++) if (_rowOrder[i].Button == owner) return RowTip(i);
        for (int g = 0; g < 5; g++) if (_groupHead[g] == owner) return GroupTip(g);
        if (owner == _bell) return _bellTip;
        if (owner == _tide) return "The tide. Boats come at high water; the ford can be walked at low water. " + _tideText.Text.Replace("\n", ", ") + ".";
        if (owner == _purse)
        {
            sb.Append(Money(t.Coin)).Append(" coin; +").Append(Money(t.CoinPerMinute)).Append(" coin a minute. ");
            sb.Append(_level.Text).Append(t.Level < t.MaxLevel ? $": renown {Money(t.Renown)} of {Money(t.NextLevelRenown)}." : ".");
            if (t.NextGift.Length > 0) sb.Append(" Next: ").Append(t.NextGift);
            return sb.ToString();
        }
        string Key(string action) { string k = Settings.GameInput.Bound(action); return k.Length > 0 ? $" [{k}]" : ""; }
        if (owner == _achMark) return $"Achievements{Key("tt_achievements")}: {_achCount.Text}.";
        if (owner == _agePageMark) return t.NextAgeRung ? $"This age's page{Key("tt_next_age")}." : "This age's page: it opens when the next age is rung in.";
        if (owner == _home) return $"Back to the town{Key("tt_home_view")}: the view follows the town again.";
        if (owner == PauseMark) return $"Pause{Key("tt_pause")}: close the book, the menu.";
        return "";
    }

    private string RowTip(int i)
    {
        var t = _drv.Town;
        var it = _shown[i];
        var sb = _tipSb.Clear();
        sb.Append(RowName(it));
        if (_rowOrder[i].Step.Text.Length > 0) sb.Append(", level ").Append(_rowOrder[i].Step.Text);
        sb.Append(". ").Append(Tooltip(it));
        int w = WorthTenths(it.Worth);
        if (w > 0)
        {
            sb.Append(' ').Append(WorthText(w));
            double perSec = it.Worth / 60.0;
            if (perSec > 0 && it.Price > 0) sb.Append("; pays for itself in ").Append(Math.Ceiling(it.Price / perSec).ToString(Inv)).Append(" s");
            sb.Append('.');
        }
        if (it.Price > 0 && !double.IsInfinity(it.Price)) sb.Append(" Price: ").Append(Money(it.Price)).Append(" coin.");
        if (!it.Affordable)
        {
            if (it.Reason == "Not enough coin")
            {
                double need = Math.Max(1, it.Price - t.Coin), rate = t.CoinPerMinute / 60.0;
                sb.Append(' ').Append(Money(need)).Append(" coin short");
                if (rate > 0) sb.Append(", about ").Append(Math.Ceiling(need / rate).ToString(Inv)).Append(" s");
                sb.Append('.');
            }
            else if (it.Reason.Length > 0) sb.Append(' ').Append(it.Reason).Append('.');
        }
        int ord = 0;
        for (int k = 0; k < i; k++) if (_shown[k].Kind == ShopItemKind.Building) ord++;
        string keys = KeyLabel(ActionFor(it, it.Kind == ShopItemKind.Building ? ord : 0), it.Kind);
        if (keys.Length > 0) sb.Append(" Key: ").Append(keys).Append('.');
        return sb.ToString();
    }

    private string GroupTip(int g)
    {
        var t = _drv.Town;
        var sb = _tipSb.Clear();
        sb.Append(GroupNames[g]).Append('.');
        bool any = false;
        foreach (var it in _items)
        {
            if ((int)GroupOf(it.Kind) != g || IsRowItem(it, t) || IsLocked(it)) continue;
            sb.Append(any ? ", " : " Done: ").Append(RowName(it));
            any = true;
        }
        if (any) sb.Append('.');
        foreach (var it in _items)
        {
            if ((int)GroupOf(it.Kind) != g || !IsLocked(it)) continue;
            sb.Append(' ').Append(RowName(it)).Append(": ").Append(Lower(it.Reason)).Append('.');
        }
        return sb.ToString();
    }
}
