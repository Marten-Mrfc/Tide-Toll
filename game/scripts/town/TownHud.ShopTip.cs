using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Sheet v3: a shop row's tooltip is a small card (296 wide), not a sentence: the title (name and step, body bold) with
/// the key cap or caps at its right; one sentence of what it does (caption, ink); a hairline; then facts in two columns
/// (label muted, value ink): Worth; Price; Short (only when not affordable); Why (only when blocked). It shares the
/// play tooltip's panel, placement and timing (TownHud.Tip.cs); only its content is laid out here.
/// </summary>
public partial class TownHud
{
    private Control _rowCard = null!;
    private Label _rcTitle = null!, _rcText = null!;
    private readonly Label[] _rcKeys = new Label[2];
    private ColorRect _rcRule = null!;
    private readonly Label[] _rcLabel = new Label[5];
    private readonly RichTextLabel[] _rcValue = new RichTextLabel[5];
    private readonly string[] _rcValueShown = new string[5];
    /// <summary>The card's parts as last made (tests read them through <see cref="RowTipParts"/>).</summary>
    private string _rtTitle = "", _rtSentence = "";
    private readonly List<string> _rtKeys = new();
    private readonly List<(string label, string value)> _rtFacts = new();
    private const float FactLabelUnits = 76;

    private void BuildRowCard(Control holder)
    {
        _rowCard = new Control { Name = "RowCard", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        holder.AddChild(_rowCard);
        _rcTitle = UiKit.Fixed(_s, UiKit.Body, UiKit.UiBold);
        _rcTitle.Name = "Title";
        _rowCard.AddChild(_rcTitle);
        for (int k = 0; k < _rcKeys.Length; k++)
        {
            _rcKeys[k] = UiKit.KeyCap(_s);
            _rcKeys[k].Name = "Key" + k;
            _rowCard.AddChild(_rcKeys[k]);
        }
        _rcText = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.Ink);
        _rcText.Name = "Text";
        _rcText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _rcText.VerticalAlignment = VerticalAlignment.Top;
        _rcText.ClipText = false;
        _rcText.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        _rowCard.AddChild(_rcText);
        _rcRule = new ColorRect { Name = "Rule", Color = UiKit.Ruling, MouseFilter = Control.MouseFilterEnum.Ignore };
        _rowCard.AddChild(_rcRule);
        for (int k = 0; k < _rcLabel.Length; k++)
        {
            _rcLabel[k] = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
            _rcLabel[k].Name = "Fact" + k;
            _rcLabel[k].VerticalAlignment = VerticalAlignment.Top;
            _rowCard.AddChild(_rcLabel[k]);
            var v = new RichTextLabel
            {
                Name = "Value" + k, MouseFilter = Control.MouseFilterEnum.Ignore, BbcodeEnabled = false, ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipContents = false, FitContent = false,
            };
            v.AddThemeFontSizeOverride("normal_font_size", UiKit.Type(_s, UiKit.Caption));
            v.AddThemeFontOverride("normal_font", UiKit.UiFont);
            v.AddThemeColorOverride("default_color", UiKit.Ink);
            _rcValue[k] = v;
            _rcValueShown[k] = "\u0001";
            _rowCard.AddChild(v);
        }
    }

    private int RowIndexOf(Control owner)
    {
        for (int i = 0; i < _rowOrder.Count; i++) if (_rowOrder[i].Button == owner) return i;
        return -1;
    }

    /// <summary>Makes the card's parts for row i and returns them as one line (the tooltip's text for tests).</summary>
    private string RowCardText(int i)
    {
        var t = _drv.Town;
        var it = _shown[i];
        if (it.Kind == ShopItemKind.Pave) it.Price = CheapestPave(it.Price);
        bool mode = it.Kind == ShopItemKind.Pave ? PaveMode : it.Kind == ShopItemKind.Extend && ExtendMode;
        _rtTitle = it.Name.Length > 0 && it.Kind == ShopItemKind.Hand ? it.Name : RowName(it);
        if (_rowOrder[i].Step.Text.Length > 0) _rtTitle += " " + _rowOrder[i].Step.Text;
        TrainingStep = _drv.Town.TrainingStepBonus;
        _rtSentence = Tooltip(it);
        _rtKeys.Clear();
        int ord = 0;
        for (int k = 0; k < i; k++) if (_shown[k].Kind == ShopItemKind.Building) ord++;
        string first = FirstKey(ActionFor(it, it.Kind == ShopItemKind.Building ? ord : 0));
        if (first.Length > 0) _rtKeys.Add(first);
        if (it.Kind == ShopItemKind.Pave) { string p = Settings.GameInput.Bound("tt_pave"); if (p.Length > 0 && p != first) _rtKeys.Add(p); }
        _rtFacts.Clear();
        int w = WorthTenths(it.Worth);
        if (w > 0)
        {
            _rtFacts.Add(("Worth", WorthLine(w)));
            // Addendum F: its own line, so no value breaks in the middle of a phrase.
            double perSec = it.Worth / 60.0;
            if (perSec > 0 && it.Price > 0 && !double.IsInfinity(it.Price)) _rtFacts.Add(("Pays back", "in " + Span(it.Price / perSec)));
        }
        if (it.Price > 0 && !double.IsInfinity(it.Price)) _rtFacts.Add(("Price", (it.Kind == ShopItemKind.Pave ? "from " : "") + CoinMark + Money(it.Price)));
        if (!it.Affordable && !mode)
        {
            if (it.Reason == "Not enough coin")
            {
                double need = Math.Max(1, it.Price - t.Coin), rate = t.CoinPerMinute / 60.0;
                string shortBy = CoinMark + Money(need);
                if (rate > 0) shortBy += ", about " + Span(need / rate);
                _rtFacts.Add(("Short", shortBy));
            }
            else if (it.Reason.Length > 0) _rtFacts.Add(("Why", it.Reason));
        }
        var sb = _tipSb.Clear();
        sb.Append(_rtTitle);
        foreach (var k in _rtKeys) sb.Append(" [").Append(k).Append(']');
        sb.Append(" | ").Append(_rtSentence);
        foreach (var (label, value) in _rtFacts) sb.Append(" | ").Append(label).Append(' ').Append(value);
        return sb.ToString();
    }

    /// <summary>"44 s", "18 min", "3 h": a time to wait, short enough not to wrap the fact's line.</summary>
    private static string Span(double seconds)
    {
        double s = Math.Ceiling(seconds);
        if (s < 120) return s.ToString(Inv) + "\u00A0s";
        if (s < 7200) return Math.Ceiling(s / 60).ToString(Inv) + "\u00A0min";
        return Math.Ceiling(s / 3600).ToString(Inv) + "\u00A0h";
    }

    /// <summary>Lays the card out for its parts and returns the tooltip's size.</summary>
    private Vector2 PlaceRowCard()
    {
        float w = UiKit.U(_s, HudZones.TipW), pad = UiKit.U(_s, 12), inner = w - 2 * pad;
        int fs = UiKit.Type(_s, UiKit.Caption);
        var f = UiKit.UiFont;
        // Title row: the key caps at the right end, the title in what is left.
        float kx = w - pad;
        for (int k = _rcKeys.Length - 1; k >= 0; k--)
        {
            var cap = _rcKeys[k];
            cap.Visible = k < _rtKeys.Count;
            if (!cap.Visible) continue;
            cap.Text = _rtKeys[k];
            float cw = Math.Max(UiKit.U(_s, 20), TextWidth(cap) + UiKit.U(_s, 8));
            kx -= cw;
            cap.Position = new Vector2(kx, pad + UiKit.U(_s, 2));
            cap.Size = new Vector2(cw, UiKit.U(_s, 20));
            kx -= UiKit.U(_s, 4);
        }
        _rcTitle.Text = _rtTitle;
        _rcTitle.Position = new Vector2(pad, pad);
        _rcTitle.Size = new Vector2(kx - pad - UiKit.U(_s, 4), UiKit.U(_s, 24));
        float y = pad + UiKit.U(_s, 28);
        _rcText.Text = _rtSentence;
        float th = f.GetMultilineStringSize(_rtSentence, HorizontalAlignment.Left, inner, fs).Y;
        _rcText.Size = new Vector2(inner, th + 2);
        th = Math.Max(th, _rcText.GetLineCount() * (f.GetHeight(fs) + _rcText.GetThemeConstant("line_spacing")));
        _rcText.Position = new Vector2(pad, y);
        _rcText.Size = new Vector2(inner, MathF.Ceiling(th) + 2);
        y += MathF.Ceiling(th) + UiKit.U(_s, 8);
        _rcRule.Position = new Vector2(pad, y);
        _rcRule.Size = new Vector2(inner, Math.Max(1, UiKit.U(_s, 1)));
        y += UiKit.U(_s, 8);
        float lx = pad + UiKit.U(_s, FactLabelUnits), vw = w - pad - lx, lineH = f.GetHeight(fs);
        for (int k = 0; k < _rcLabel.Length; k++)
        {
            bool on = k < _rtFacts.Count;
            _rcLabel[k].Visible = on;
            _rcValue[k].Visible = on;
            if (!on) continue;
            var (label, value) = _rtFacts[k];
            _rcLabel[k].Text = label;
            _rcLabel[k].Position = new Vector2(pad, y);
            _rcLabel[k].Size = new Vector2(lx - pad - UiKit.U(_s, 4), lineH);
            if (_rcValueShown[k] != value) { _rcValueShown[k] = value; WriteCoinText(_rcValue[k], value); }
            float vh = Math.Max(lineH, f.GetMultilineStringSize(value.Replace(CoinMark.ToString(), "00\u00A0"), HorizontalAlignment.Left, vw, fs).Y);
            _rcValue[k].Position = new Vector2(lx, y);
            _rcValue[k].Size = new Vector2(vw, vh + 2);
            vh = Math.Max(vh, _rcValue[k].GetContentHeight());
            _rcValue[k].Size = new Vector2(vw, vh + 2);
            y += vh + UiKit.U(_s, 2);
        }
        _rowCard.Position = Vector2.Zero;
        _rowCard.Size = new Vector2(w, MathF.Ceiling(y + pad));
        return _rowCard.Size;
    }

    /// <summary>A text with coin marks as the coin icon, into a rich label.</summary>
    private void WriteCoinText(RichTextLabel l, string text)
    {
        l.Clear();
        var coin = IconOr("ui-coin", "ui-coin");
        int px = UiKit.Type(_s, UiKit.Caption);
        int from = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != CoinMark) continue;
            if (i > from) l.AddText(text.Substring(from, i - from));
            // Follow-up 5: about 4 units of air between the coin sign and the number, as in the pill (a no-break space).
            if (i < text.Length) { l.AddImage(coin, px, px, null, InlineAlignment.Center); l.AddText("\u00A0"); }
            from = i + 1;
        }
    }

    /// <summary>The card for row i as it would show (tests): title, keys, sentence and the fact lines.</summary>
    public (string title, string[] keys, string sentence, (string label, string value)[] facts, string text) RowTipParts(int i)
    {
        string text = RowCardText(i);
        return (_rtTitle, _rtKeys.ToArray(), _rtSentence, _rtFacts.ToArray(), text);
    }
}
