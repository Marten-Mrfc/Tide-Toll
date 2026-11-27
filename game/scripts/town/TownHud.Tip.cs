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
/// Last look: the box grows with its text (up to <see cref="TipMaxLines"/> lines at its width, wider beyond that) and
/// never cuts it; amounts carry the coin sign (the shop's coin icon, written as <see cref="CoinMark"/> in the text),
/// never the word; a boon's tip lies under the choice card, never over the other boon.
/// </summary>
public partial class TownHud
{
    public const float TipDelay = 0.25f, TipFadeIn = 0.12f, TipRefresh = 0.5f;
    private PanelContainer _tip = null!;
    private RichTextLabel _tipLabel = null!;
    private string _tipText = "";
    /// <summary>Stands for the coin sign in a tooltip's text.</summary>
    public const char CoinMark = '\u00A4';
    public const int TipMaxLines = 5;
    private Control? _tipOwner;
    private float _tipAge, _tipRefresh;
    private readonly StringBuilder _tipSb = new();

    /// <summary>The tooltip's owner, text and rectangle while it shows (tests).</summary>
    public Control? TipOwner => _tip.Visible ? _tipOwner : null;
    public string TipText => _tip.Visible ? _tipText : "";
    public Rect2 TipShownRect => _tip.Visible ? _tip.GetGlobalRect() : new Rect2();

    private void BuildTip()
    {
        _tip = new PanelContainer { Name = "Tooltip", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _tip.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _tip.AddChild(holder);
        _tipLabel = new RichTextLabel
        {
            Name = "Text", MouseFilter = Control.MouseFilterEnum.Ignore, BbcodeEnabled = false, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipContents = false, FitContent = false,
        };
        _tipLabel.AddThemeFontSizeOverride("normal_font_size", UiKit.Type(_s, UiKit.Caption));
        _tipLabel.AddThemeFontOverride("normal_font", UiKit.UiFont);
        _tipLabel.AddThemeColorOverride("default_color", UiKit.Ink);
        holder.AddChild(_tipLabel);
        BuildRowCard(holder);
        _root.AddChild(_tip);
        _tipOwner = null;
    }

    /// <summary>The owner under the pointer, and the side its tip opens on.</summary>
    private Control? TipOwnerAt(Vector2 p, out HudZones.TipSide side)
    {
        side = HudZones.TipSide.Left;
        if (_main.Menu != null && _main.Menu.IsPaused) return null;
        // Night 3, L2: the rest of the play interface has this tooltip too (no engine tooltip is left in play).
        if (AchievementsOpen)
        {
            side = HudZones.TipSide.Right;
            for (int i = 0; i < _achCell.Length; i++) if (_achCell[i] != null && Hit(_achCell[i], p)) return _achCell[i];
        }
        if (_choice.Visible && _choiceF.On)
        {
            side = HudZones.TipSide.Right;
            if (Hit(_boonBtn[0], p)) return _boonBtn[0];
            side = HudZones.TipSide.Left;
            if (Hit(_boonBtn[1], p)) return _boonBtn[1];
        }
        if (_shop.IsVisibleInTree() && _shop.Modulate.A > 0.5f && _shop.GetGlobalRect().HasPoint(p))
        {
            foreach (var r in _rowOrder) if (r.Button.Visible && r.Alpha > 0.5f && r.Button.GetGlobalRect().HasPoint(p)) return r.Button;
            // Sheet v3: the headings carry nothing; the foot line's tooltip lists what is still to come and what is done.
            if (_foot.Visible && _foot.GetGlobalRect().HasPoint(p)) return _foot;
            return null;
        }
        side = HudZones.TipSide.Right;
        // n7 trades: the Trades card's lines, pills and switch, and the villager card's Trade caps.
        if (TradesTipOwner(p) is { } tro) return tro;
        if (PersonCapOwner(p) is { } cap) return cap;
        if (Hit(_bell, p)) return _bell;
        if (Hit(_tide, p)) return _tide;
        if (_ledgerPanel.Modulate.A > 0.5f && Hit(_purse, p)) return _purse;
        if (_ledgerPanel.Modulate.A > 0.5f && _req.Visible && Hit(_goalBox, p)) return _goalBox;
        if (_ledgerPanel.Modulate.A > 0.5f && (_drive.Visible || _nextAge.Visible) && Hit(_stripBox, p)) return _stripBox;
        if (_ledgerPanel.Modulate.A > 0.5f && _notes.Showing && Hit(_noteBox, p)) return _noteBox;
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
        if (owner != _tipOwner) { _tipOwner = owner; _tipAge = 0; _tip.Visible = false; _tradeTipFor = null; }
        if (owner == null) return;
        _tipAge += dt;
        if (_tipAge < TipDelay) return;
        _tipRefresh -= dt;
        if (!_tip.Visible || _tipRefresh <= 0)
        {
            _tipRefresh = TipRefresh;
            int row = RowIndexOf(owner);
            string text = row >= 0 ? RowCardText(row) : TipTextFor(owner);
            if (text.Length == 0) { _tip.Visible = false; return; }
            _rowCard.Visible = row >= 0;
            _tipLabel.Visible = row < 0;
            Vector2 size = row >= 0 ? PlaceRowCard() : PlaceTipText(text);
            _tipText = text;
            Rect2 r;
            if (_choice.Visible && (owner == _boonBtn[0] || owner == _boonBtn[1]))
            {
                // Under the choice card, below its own half: never over the other boon or the card's text.
                var card = _choice.GetGlobalRect();
                var vis = GetViewport().GetVisibleRect().Size;
                float x = owner == _boonBtn[0] ? card.Position.X : card.End.X - size.X;
                x = Math.Max(UiKit.U(_s, 4), Math.Min(x, vis.X - size.X - UiKit.U(_s, 4)));
                r = new Rect2(new Vector2(x, card.End.Y + UiKit.U(_s, HudZones.Gap)).Round(), size);
            }
            else
            {
                var avoid = _choice.Visible ? _choice.GetGlobalRect() : _banner.Visible ? _banner.GetGlobalRect() : new Rect2();
                // Addendum F: a row's card stands 8 away from the sheet, not from the row inside it.
                var o = owner.GetGlobalRect();
                if (row >= 0) { var sh = _shop.GetGlobalRect(); o = new Rect2(sh.Position.X, o.Position.Y, o.End.X - sh.Position.X, o.Size.Y); }
                r = Zones.TipRect(o, size, side, avoid);
            }
            _tip.Position = r.Position;
            _tip.Size = r.Size;
            _tip.Visible = true;
        }
        _tip.Modulate = new Color(1, 1, 1, Math.Clamp((_tipAge - TipDelay) / TipFadeIn, 0, 1));
    }

    /// <summary>The plain tooltip: its text into the label, the box as tall as the text (wider past five lines).</summary>
    private Vector2 PlaceTipText(string text)
    {
        if (_tipText != text || _tipLabel.GetParsedText().Length == 0) WriteTip(text);
        float w = UiKit.U(_s, HudZones.TipW), pad = UiKit.U(_s, 12);
        var f = UiKit.UiFont;
        int fs = UiKit.Type(_s, UiKit.Caption);
        // Measured with the coin sign as wide as two figures; past five lines the box widens instead (up to 1.6 x).
        string measure = text.Replace(CoinMark.ToString(), "00");
        float lineH = f.GetHeight(fs);
        float h = f.GetMultilineStringSize(measure, HorizontalAlignment.Left, w - 2 * pad, fs).Y;
        for (int k = 0; k < 6 && h > lineH * TipMaxLines + 1f && w < UiKit.U(_s, HudZones.TipW * 1.6f); k++)
        {
            w += UiKit.U(_s, HudZones.TipW * 0.1f);
            h = f.GetMultilineStringSize(measure, HorizontalAlignment.Left, w - 2 * pad, fs).Y;
        }
        _tipLabel.Position = new Vector2(pad, pad);
        _tipLabel.Size = new Vector2(w - 2 * pad, h + 2);
        h = Math.Max(h, _tipLabel.GetContentHeight());
        // A one-line tip is as wide as its text (sheet v3: "Achievements · Tab" is a small slip, not a wide card).
        if (h <= lineH + 1)
        {
            float tw = f.GetStringSize(measure, HorizontalAlignment.Left, -1, fs).X;
            w = Math.Min(w, MathF.Ceiling(tw) + 2 * pad + 4);
            _tipLabel.Size = new Vector2(w - 2 * pad, _tipLabel.Size.Y);
        }
        _tipLabel.Size = new Vector2(w - 2 * pad, h + 2);
        return new Vector2(w, MathF.Ceiling(h) + 2 * pad + 2);
    }

    /// <summary>The tooltip's text into its label, the coin marks as the coin icon (only when the text changes).</summary>
    private void WriteTip(string text)
    {
        _tipLabel.Clear();
        var coin = IconOr("ui-coin", "ui-coin");
        int px = UiKit.Type(_s, UiKit.Caption);
        int from = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != CoinMark) continue;
            if (i > from) _tipLabel.AddText(text.Substring(from, i - from));
            if (i < text.Length) _tipLabel.AddImage(coin, px, px, null, InlineAlignment.Center);
            from = i + 1;
        }
    }

    /// <summary>"+[coin]5.4 a minute": a worth with the coin sign (tenths below 10).</summary>
    private static string TipWorth(int tenths) => tenths < 100 && tenths % 10 != 0 ? $"+\u2060{CoinMark}{tenths / 10}.{tenths % 10} a minute" : $"+\u2060{CoinMark}{Money(tenths / 10)} a minute";

    private string TipTextFor(Control owner)
    {
        var t = _drv.Town;
        var sb = _tipSb.Clear();
        if (owner == _foot) return FootTip();
        if (TradesTipText(owner) is { } trs) return trs;
        if (PersonCapTip(owner) is { } cps) return cps;
        if (owner == _bell) return _bellTip;
        if (owner == _tide) return "The tide. Boats come at high water; the ford can be walked at low water. " + _tideText.Text.Replace("\n", ", ") + ".";
        if (owner == _purse)
        {
            sb.Append(CoinMark).Append(Money(t.Coin)).Append("; +\u2060").Append(CoinMark).Append(Money(t.CoinPerMinute)).Append(" a minute. ");
            sb.Append(_level.Text).Append(t.Level < t.MaxLevel ? $": renown {Money(t.Renown)} of {Money(t.NextLevelRenown)}." : ".");
            if (t.NextGift.Length > 0) sb.Append(" Next: ").Append(t.NextGift);
            // n7 trades: since the retune a second-age level's renown eases with time (the number after "of" falls slowly).
            if (t.InRiverAge && t.Level < t.MaxLevel) sb.Append(" Word of the river spreads: each level asks a little less as time goes by.");
            return sb.ToString();
        }
        string Key(string action) { string k = Settings.GameInput.Bound(action); return k.Length > 0 ? $" [{k}]" : ""; }
        for (int i = 0; i < _achCell.Length; i++)
            if (_achCell[i] == owner && i < t.AchievementCount) { var a = t.Achievement(i); return $"{a.Name}: {a.Description}"; }
        for (int side = 0; side < 2; side++)
            if (_boonBtn[side] == owner && t.ChoiceOpen)
            {
                var c = t.Choice;
                return $"{(side == 0 ? c.AName : c.BName)}{Key(side == 0 ? "tt_choose_a" : "tt_choose_b")}: {Town.BoonText(side == 0 ? c.A : c.B)} For good; the town runs on while you choose.";
            }
        if (owner == _goalBox) return RequestCardText.Replace(" | ", ". ") + ".";
        if (owner == _stripBox)
            return _nextAge.Visible ? $"Ring in the next age{Key("tt_next_age")}: the town moves on to its next age when you are ready."
                : DriveCardText.Replace(" | ", ". ").Replace("Charter. ", "") + ".";
        if (owner == _noteBox) return NoteShowing + " Click to put it away.";
        // Sheet v3: a navigation button's tooltip names it and its key.
        if (owner == _achMark) return NavTip("Achievements", "tt_achievements");
        if (owner == _agePageMark) return NavTip("The age's page", "tt_next_age");
        if (owner == _home) return NavTip("Centre the view", "tt_home_view");
        if (owner == PauseMark) return NavTip("Pause", "tt_pause");
        return "";
    }

    /// <summary>"Achievements · Tab" (the name alone when the action has no key).</summary>
    public static string NavTip(string name, string action)
    {
        string k = Settings.GameInput.Bound(action);
        return k.Length > 0 ? $"{name} · {k}" : name;
    }
}
