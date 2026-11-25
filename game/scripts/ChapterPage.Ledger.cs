using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The chapter page's river part (river rules on): "The ledger" with the coin glyph, the purse, income today,
/// reputation with what it means in words, boats served and turned back today, the river's state in words (open water,
/// "Spring flood, day N" with the ford closed, mud on the banks, frozen), today's grain (brought to the landing and
/// dropped on the way), how many walkers had to wait at each crossing today out of those who crossed there, and the
/// chronicle: the latest four events as margin notes in a small serif hand, the newest darkest. Values change only when
/// they do; today's counts are the simulation's running totals less a snapshot taken at the day's start.
/// </summary>
public partial class ChapterPage
{
    private Control? _ledgerHead, _chronicle;
    private Label _coin = null!, _today = null!, _rep = null!, _repNote = null!, _boats = null!;
    private Label _river = null!, _grain = null!, _waited = null!;
    private const int Slots = 16;
    private long _ledgerDay = -1;
    private int _grain0, _lost0, _lastGrain = -1, _lastLost = -1, _lastRiverState = -1, _lastRiverDay = -1;
    private readonly int[] _xId = new int[Slots], _xCrossed0 = new int[Slots], _xWaited0 = new int[Slots];
    private readonly int[] _shownCrossed = new int[Slots], _shownWaited = new int[Slots], _shownType = new int[Slots];
    private int _shownCount = -1;
    private readonly System.Text.StringBuilder _sb = new(128);
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
        _river = Entry(col, "The river", out _, false);
        _grain = Entry(col, "Grain today", out _, false);
        _waited = Entry(col, "Waited to cross", out _, false);
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
    public void RefreshRiver(RiverView r, double time, double daySec, RiverFeed feed)
    {
        if (_ledgerHead == null) return;
        TrackDay(r, time, daySec);
        RiverToday(r, feed);
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

    /// <summary>The river's state, today's grain and the walkers who waited at each crossing today (in words).</summary>
    /// <summary>
    /// Snapshots the running totals when a new day starts (called every frame, so the snapshot is at most a frame late;
    /// cheap and allocation-free); also when a new world's totals fall below the snapshot.
    /// </summary>
    public void TrackDay(RiverView r, double time, double daySec)
    {
        if (_ledgerHead == null) return;
        var st = r.Stats;
        var xs = r.Crossings;
        long day = (long)System.Math.Floor(time / daySec + 1e-9);
        bool rebase = day != _ledgerDay || st.GrainDelivered < _grain0 || st.GrainLost < _lost0;
        if (rebase)
        {
            _ledgerDay = day;
            _grain0 = st.GrainDelivered; _lost0 = st.GrainLost;
            for (int i = 0; i < Slots; i++) _xId[i] = -1;
            for (int i = 0; i < xs.Count && i < Slots; i++) { _xId[i] = xs[i].Id; _xCrossed0[i] = xs[i].WalkersCrossed; _xWaited0[i] = xs[i].WalkersWaited; }
        }
    }

    private void RiverToday(RiverView r, RiverFeed feed)
    {
        var st = r.Stats;
        var xs = r.Crossings;
        int state = r.Frozen ? 3 : r.Flooded ? 1 : r.Muddy ? 2 : 0;
        int sday = state == 3 ? feed.FrozenDay : state == 1 ? feed.FloodDay : 0;
        if (state != _lastRiverState || sday != _lastRiverDay)
        {
            _lastRiverState = state; _lastRiverDay = sday;
            _river.Text = state switch
            {
                1 => sday > 0 ? $"Spring flood, day {sday} · the ford is closed" : "Spring flood · the ford is closed",
                2 => "Mud on the banks after the flood",
                3 => sday > 0 ? $"Frozen since day {sday} · walk across anywhere" : "Frozen · walk across anywhere",
                _ => "Open water",
            };
        }
        int grain = st.GrainDelivered - _grain0, lost = st.GrainLost - _lost0;
        if (grain != _lastGrain || lost != _lastLost)
        {
            _lastGrain = grain; _lastLost = lost;
            _grain.Text = lost > 0 ? $"{grain} sacks to the landing · {lost} dropped on the way" : $"{grain} sacks to the landing";
        }
        // Per crossing: walkers who waited today out of those who crossed there today. Built only when a count moved.
        bool changed = xs.Count != _shownCount;
        for (int i = 0; i < xs.Count && i < Slots && !changed; i++)
        {
            var (c, w) = Today(xs[i], i);
            changed = c != _shownCrossed[i] || w != _shownWaited[i] || (int)xs[i].Type != _shownType[i];
        }
        if (!changed) return;
        _shownCount = xs.Count;
        _sb.Clear();
        for (int i = 0; i < xs.Count && i < Slots; i++)
        {
            var (c, w) = Today(xs[i], i);
            _shownCrossed[i] = c; _shownWaited[i] = w; _shownType[i] = (int)xs[i].Type;
            if (c == 0) continue; // a crossing nobody used today says nothing
            if (_sb.Length > 0) _sb.Append(" · ");
            _sb.Append(World.CrossingName(xs[i].Type)).Append(' ');
            if (xs[i].Type == CrossingType.Ford) _sb.Append(c).Append(" waded"); // nobody waits at a ford
            else _sb.Append(w).Append(" of ").Append(c);
        }
        _waited.Text = _sb.Length == 0 ? "nobody has crossed yet" : _sb.ToString();
    }

    private (int crossed, int waited) Today(Crossing x, int _)
    {
        int c0 = 0, w0 = 0; // a crossing built today started from nothing
        for (int k = 0; k < Slots; k++) if (_xId[k] == x.Id) { c0 = _xCrossed0[k]; w0 = _xWaited0[k]; break; }
        return (System.Math.Max(0, x.WalkersCrossed - c0), System.Math.Max(0, x.WalkersWaited - w0));
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
