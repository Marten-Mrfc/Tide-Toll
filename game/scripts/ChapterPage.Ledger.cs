using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The chapter page's river part (river rules on): "The ledger" with the coin glyph, the purse, income today,
/// reputation with what it means in words, boats served and turned back today, and the chronicle: the latest four
/// events as margin notes in a small serif hand, the newest darkest. Values change only when they do.
/// </summary>
public partial class ChapterPage
{
    private Control? _ledgerHead, _chronicle;
    private Label _coin = null!, _today = null!, _rep = null!, _repNote = null!, _boats = null!;
    private readonly Label[] _notes = new Label[RiverFeed.ChronicleLines];
    private int _lastCoin = int.MinValue, _lastToday = int.MinValue, _lastRep = int.MinValue, _lastServed = -1, _lastTurned = -1, _lastNotes = -1;

    /// <summary>The river ledger (coin, income today, reputation, boats served and turned back today) and the chronicle.</summary>
    private void BuildLedger(VBoxContainer col)
    {
        var head = new HBoxContainer { Name = "LedgerHead", MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        head.AddChild(Glyph("ui-coin"));
        var h = Heading("The ledger");
        head.AddChild(h);
        col.AddChild(head);
        _ledgerHead = head;
        _coin = Entry(col, "Coin", out _, false);
        _today = Entry(col, "Income today", out _, false);
        _rep = Entry(col, "Reputation", out _repNote);
        _boats = Entry(col, "Boats today", out _, false);
        var chron = new VBoxContainer { Name = "Chronicle", MouseFilter = MouseFilterEnum.Ignore };
        chron.AddThemeConstantOverride("separation", UiKit.Px(_s, 1));
        for (int i = 0; i < _notes.Length; i++)
        {
            // Margin notes: the river's latest events in a small serif hand, the newest darkest.
            var l = Para("", 14, UiKit.Serif, i == 0 ? UiKit.Ink : UiKit.FadedInk.Darkened(0.25f));
            l.Name = $"Note{i}";
            l.Visible = false;
            chron.AddChild(l);
            _notes[i] = l;
        }
        col.AddChild(chron);
        _chronicle = chron;
    }

    private TextureRect Glyph(string icon) => new()
    {
        Texture = UiKit.Icon(icon, UiKit.Px(_s, 20)), CustomMinimumSize = new Vector2(UiKit.Px(_s, 20), UiKit.Px(_s, 20)),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps, SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };

    /// <summary>The ledger's figures, only where a value changed (called about twice a second).</summary>
    public void RefreshRiver(RiverView r)
    {
        if (_ledgerHead == null) return;
        int coin = (int)System.Math.Floor(r.Money + 1e-9);
        if (coin != _lastCoin) { _lastCoin = coin; _coin.Text = coin + "c"; }
        int today = (int)System.Math.Floor(r.IncomeDay + 1e-9);
        if (today != _lastToday) { _lastToday = today; _today.Text = (today >= 0 ? "+" : "") + today + "c"; }
        int rep = (int)System.Math.Round(r.Reputation);
        if (rep != _lastRep)
        {
            _lastRep = rep;
            _rep.Text = rep + " of 100";
            // In words: boats come more often with a good name (frequency x (0.5 + rep / 100)).
            double f = 0.5 + rep / 100.0;
            _repNote.Text = (rep >= 70 ? "well thought of" : rep >= 40 ? "fair" : "boatmen avoid us") + $" · boats come {f:0.0}x as often";
        }
        if (r.ServedToday != _lastServed || r.TurnedBackToday != _lastTurned)
        {
            _lastServed = r.ServedToday; _lastTurned = r.TurnedBackToday;
            _boats.Text = $"{r.ServedToday} served · {r.TurnedBackToday} turned back";
        }
    }

    /// <summary>The chronicle's margin notes from the river feed (on a new note only).</summary>
    public void SetNotes(RiverFeed feed)
    {
        if (_chronicle == null || feed.NotesWritten == _lastNotes) return;
        _lastNotes = feed.NotesWritten;
        for (int i = 0; i < _notes.Length; i++)
        {
            string t = feed.Note(i);
            _notes[i].Text = t;
            // A folded note stays folded; the others show when they have text.
            int at = System.Array.IndexOf(_optional, _notes[i]);
            bool folded = at >= 0 && at < Folded;
            _notes[i].Visible = t.Length > 0 && !folded;
        }
    }

}
