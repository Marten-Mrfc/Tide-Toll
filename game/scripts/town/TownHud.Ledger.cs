using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, sections 2 and 3): the ledger, one paper sheet at the top left with four ruled sections
/// that never change size: the purse (coin, income, level with renown over its bar, what the next level gives), the
/// goal (the request card's content, TownHud.Request.cs), the strip (Ring in the next age, or the charter drive) and the
/// note (one note at a time, two lines; the others wait in a plain first-in-first-out queue). Zoomed out the purse
/// stays as one line in its place (the slim purse).
/// </summary>
public partial class TownHud
{
    private Panel _ledgerPanel = null!;
    private Control _purse = null!, _goalBox = null!, _stripBox = null!, _noteBox = null!;
    private Label _noteLabel = null!;
    private float _noteAge = -1f;

    /// <summary>The note showing now ("" none) and how many wait (tests).</summary>
    public string NoteShowing => _notes.Showing ? _notes.Current.Text : "";
    public int NotesWaiting => _notes.Waiting;
    public Rect2 PurseRect => _purse.GetGlobalRect();
    public Rect2 LedgerRect => _ledgerPanel.GetGlobalRect();
    /// <summary>The goal section while a request shows in it (empty otherwise).</summary>
    public Rect2 RequestRect => _req.Visible ? _goalBox.GetGlobalRect() : new Rect2();

    private void BuildLedger()
    {
        var z = Zones;
        float s = _s;
        int pad = UiKit.U(s, 12), w = (int)z.Ledger.Size.X, inner = w - 2 * pad;
        _ledgerPanel = new Panel { Name = "Ledger", MouseFilter = Control.MouseFilterEnum.Stop };
        _ledgerPanel.AddThemeStyleboxOverride("panel", UiKit.CardBox(s));
        _ledgerPanel.Position = z.Ledger.Position;
        _ledgerPanel.Size = z.Ledger.Size;
        _root.AddChild(_ledgerPanel);
        _ledger = _ledgerPanel;
        float y0 = z.Ledger.Position.Y;
        Control Section(string name, Rect2 r)
        {
            var c = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Pass, Position = new Vector2(0, r.Position.Y - y0), Size = new Vector2(w, r.Size.Y), ClipContents = true };
            _ledgerPanel.AddChild(c);
            return c;
        }
        // Faint ruled lines across the sheet (ink at 8 percent, one every 24): blank paper reads as an account book.
        var ruling = new RuledLines { Name = "Ruling", Gap = UiKit.U(s, 24), Inset = pad, Position = new Vector2(0, z.Goal.Position.Y - y0), Size = new Vector2(w, z.Ledger.End.Y - z.Goal.Position.Y - pad / 2f) };
        _ledgerPanel.AddChild(ruling);
        _purse = Section("Purse", z.Purse);
        _goalBox = Section("Goal", z.Goal);
        _stripBox = Section("Strip", z.Strip);
        _noteBox = Section("NoteSection", z.Note);
        // The rules between the sections: 1 px, muted, in the 8 of air.
        foreach (var r in new[] { z.Goal, z.Strip, z.Note })
        {
            var rule = UiKit.MutedRule();
            rule.Position = new Vector2(pad, r.Position.Y - y0 - UiKit.U(s, 4) - 1);
            rule.Size = new Vector2(inner, 1);
            _ledgerPanel.AddChild(rule);
        }

        Label At(Control parent, Label l, string name, float x, float y, float lw, float h)
        {
            l.Name = name; l.Position = new Vector2(x, UiKit.U(s, y)); l.Size = new Vector2(lw, UiKit.U(s, h));
            parent.AddChild(l);
            return l;
        }
        var coinIcon = UiKit.IconRect(s, IconOr("ui-coin", "ui-coin"));
        coinIcon.Position = new Vector2(pad, UiKit.U(s, 7));
        _purse.AddChild(coinIcon);
        _coin = At(_purse, UiKit.Fixed(s, UiKit.Large, UiKit.UiBold), "Coin", pad + UiKit.U(s, 32), 0, inner - UiKit.U(s, 32), 36);
        _rate = At(_purse, UiKit.Fixed(s, UiKit.Body), "Rate", pad, 35, inner, 20);
        _level = At(_purse, UiKit.Fixed(s, UiKit.Body), "Level", pad, 54, inner, 21);
        _renown = At(_purse, UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber, HorizontalAlignment.Right), "Renown", pad, 54, inner, 21);
        _barBack = UiKit.Bar(s, UiKit.Wheat, out _barFill);
        _barBack.Name = "LevelBar";
        _barBack.Position = new Vector2(pad, UiKit.U(s, 80)); // L1 fix: 5 of air under "Level 6 of 16", not an underline
        _barBack.Size = new Vector2(inner, UiKit.U(s, 8));
        _purse.AddChild(_barBack);
        _next = At(_purse, UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted), "Next", pad, 90, inner, 20);

        _noteLabel = UiKit.Fixed(s, UiKit.Caption);
        _noteLabel.Name = "Note";
        _noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // Two lines by the sheet; a third (tighter lines) until the message round rewrites notes over 2 lines (90
        // characters of caption need about 2.3 lines of the rail).
        _noteLabel.MaxLinesVisible = 2;
        _noteLabel.AddThemeConstantOverride("line_spacing", -UiKit.U(s, 3));
        _noteLabel.ClipText = true;
        _noteLabel.VerticalAlignment = VerticalAlignment.Top;
        _noteLabel.Position = new Vector2(pad, UiKit.U(s, 2));
        _noteLabel.Size = new Vector2(inner, UiKit.U(s, 54));
        _noteLabel.Visible = false;
        _noteBox.AddChild(_noteLabel);
        _noteAge = -1f;
    }

    private HudZones Zones => new(_built, _s);

    private void BuildSlim()
    {
        var z = Zones;
        _slim = new PanelContainer { Name = "Slim", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _slim.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        _slim.Position = z.Purse.Position;
        _slim.Size = new Vector2(z.Purse.Size.X, UiKit.U(_s, 40));
        var icon = UiKit.IconRect(_s, IconOr("ui-coin", "ui-coin"));
        icon.Position = new Vector2(UiKit.U(_s, 12), UiKit.U(_s, 8));
        _slimText = UiKit.Fixed(_s, UiKit.Body, UiKit.UiBold);
        _slimText.Name = "SlimText";
        _slimText.Position = new Vector2(UiKit.U(_s, 44), 0);
        _slimText.Size = new Vector2(z.Purse.Size.X - UiKit.U(_s, 56), UiKit.U(_s, 40));
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _slim.AddChild(holder);
        holder.AddChild(icon);
        holder.AddChild(_slimText);
        _root.AddChild(_slim);
        _shownCoin = -1; _shownRate = -1; _shownRenown = -1; _shownNextRenown = -1; _shownLevel = -1;
    }

    // Last values shown, so text is rebuilt only when a value changes (round 2: no strings built per frame).
    private long _shownCoin = -1, _shownRate = -1, _shownRenown = -1, _shownNextRenown = -1;
    private int _shownLevel = -1;
    private bool _shownRung;

    private void Refresh()
    {
        var t = _drv.Town;
        long coin = (long)Math.Floor(t.Coin), rate = (long)Math.Floor(t.CoinPerMinute);
        bool coinChanged = coin != _shownCoin;
        if (coinChanged) { _shownCoin = coin; _coin.Text = Money(coin); }
        if (rate != _shownRate) { _shownRate = rate; _rate.Text = "+" + Money(rate) + " a minute"; }
        bool levelChanged = t.Level != _shownLevel || t.NextAgeRung != _shownRung;
        if (levelChanged)
        {
            _shownLevel = t.Level; _shownRung = t.NextAgeRung;
            _level.Text = t.Level >= t.MaxLevel ? $"Level {t.Level}, the last" : $"Level {t.Level} of {t.MaxLevel}";
            _next.Text = t.NextGift.Length == 0 || t.NextGift.StartsWith("Next") ? t.NextGift : "Next: " + t.NextGift;
        }
        UiKit.SetBar(_barBack, _barFill, (float)Math.Clamp(t.LevelBar, 0, 1));
        long renown = (long)Math.Floor(t.Renown), nextRenown = t.Level >= t.MaxLevel ? -1 : (long)Math.Floor(t.NextLevelRenown);
        if (renown != _shownRenown || nextRenown != _shownNextRenown || levelChanged)
        {
            _shownRenown = renown; _shownNextRenown = nextRenown;
            _renown.Text = nextRenown < 0 ? Money(renown) : $"{Money(renown)} of {Money(nextRenown)}";
            // Dropped whole, never cut, when it does not fit beside the level (it is always in the tooltip).
            _renown.Visible = TextWidth(_level) + TextWidth(_renown) + UiKit.U(_s, 12) <= _renown.Size.X;
        }
        if (coinChanged || levelChanged) _slimText.Text = $"{Money(coin)}  ·  Level {t.Level} of {t.MaxLevel}";
    }

    /// <summary>The width a label's text needs at its own font and size.</summary>
    public static float TextWidth(Label l) => TextWidth(l, l.Text);

    /// <summary>The width a text would need in this label (cached StringNames: no allocation for the names).</summary>
    public static float TextWidth(Label l, string text)
    {
        var f = l.GetThemeFont(FontName);
        int px = l.GetThemeFontSize(FontSizeName);
        return f.GetStringSize(text, HorizontalAlignment.Left, -1, px).X;
    }

    public static readonly StringName FontName = "font", FontSizeName = "font_size";
}

/// <summary>The ledger's faint ruled lines (drawn once per size).</summary>
public partial class RuledLines : Control
{
    public float Gap = 24, Inset = 12;
    public RuledLines() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }

    public override void _Draw()
    {
        for (float y = Gap; y < Size.Y; y += Gap) DrawLine(new Vector2(Inset, y), new Vector2(Size.X - Inset, y), UiKit.Ruling, 1f);
    }
}
