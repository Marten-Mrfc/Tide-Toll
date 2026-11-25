using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The left-hand page as the chapter page, written in ink on the page itself: "Chapter I: The Hamlet" in the display
/// face, how to play, the village facts (day, season and year, walkers out of total, map seed), the path health
/// figures (detour ratio, trail coverage, trunk share with its 30% to 70% band drawn as a gauge) and the ground
/// legend (the heat legend while the wear overlay is on), then the status line as a margin note.
/// Screen-space Controls, laid over the page's rectangle on screen each frame (crisper than a SubViewport on the
/// page and with no resampling). When too little of the page is on screen (zoomed in) it folds into a paper slip in
/// the top-left corner with the facts, figures and status only. Text is rebuilt about twice a second and only for
/// values that changed, so it allocates a few bytes a frame at most.
/// </summary>
public partial class ChapterPage : PanelContainer
{
    private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
    public const string HowTo =
        "Villagers walk from their houses to the well, the field and the market, and wear the grass into trails. " +
        "Draw fences to push the trails aside, or lay a footbridge across the river; paths nobody uses grow back. " +
        "With the flow preview on, a tool shows the routes that would change: dashed is now, solid is after.";
    public const string Keys =
        "Each bookmark shows its key. Esc or a right-click puts a tool down; Home turns between the book and the map; " +
        "the arrows or a middle-drag pan, the wheel zooms.";

    private float _s;
    private Label _day = null!, _walkers = null!, _seed = null!, _detour = null!, _detourNote = null!, _cover = null!, _coverNote = null!;
    private Label _trunk = null!, _trunkNote = null!, _status = null!, _preview = null!;
    private Control _title = null!, _howTo = null!, _ground = null!, _heat = null!, _legendHead = null!, _legend = null!;
    private Control? _ledgerHead, _ledgerBox, _chronicle;
    private Label _coin = null!, _today = null!, _rep = null!, _repNote = null!, _boats = null!;
    private readonly Label[] _notes = new Label[RiverFeed.ChronicleLines];
    private int _lastCoin = int.MinValue, _lastToday = int.MinValue, _lastRep = int.MinValue, _lastServed = -1, _lastTurned = -1, _lastNotes = -1;
    private BandGauge _gauge = null!;
    private VBoxContainer _col = null!;
    private Label _howPara = null!, _keysPara = null!;
    // Parts that fold away, in this order, when the page is too short for everything (1280 x 720 book view):
    // the key reminder (every tab shows its key), then the how-to paragraph (the tabs' tooltips say it too).
    private Control[] _optional = System.Array.Empty<Control>();
    private readonly float[] _optionalH = new float[8];
    private StyleBox _onPage = null!, _slip = null!;
    private Rect2 _placed;
    private int _lastDay = -1, _lastSeason = -1, _lastYear = -1, _lastOut = -1, _lastWalkers = -1, _lastSeed = -1;
    private int _lastDetour = int.MinValue, _lastCover = int.MinValue, _lastTrunk = int.MinValue;
    private double _bellUntil;
    private bool _bellShown, _overlay;

    /// <summary>True when the page is too far off screen and the entries sit on a slip in the corner.</summary>
    public bool Slip { get; private set; }
    /// <summary>The page's rectangle on screen (pixels), and how many optional parts are folded away to fit it.</summary>
    public Rect2 PageRect { get; private set; }
    public int Folded { get; private set; }
    /// <summary>Least distance in pixels kept between the page's edge and any line on it (UX: at least 12 px).</summary>
    public const float MinInset = 12f;

    public void Build(float s, bool river = false)
    {
        _s = s;
        Name = "Chapter";
        MouseFilter = MouseFilterEnum.Ignore;
        _onPage = new StyleBoxEmpty();
        _slip = UiKit.Card(s, UiKit.Paper, 1.5f);
        AddThemeStyleboxOverride("panel", _onPage);
        var col = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
        _col = col;
        col.AddThemeConstantOverride("separation", UiKit.Px(s, 3));
        AddChild(col);

        var title = new VBoxContainer { Name = "Title", MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeConstantOverride("separation", 0);
        var t1 = UiKit.Text("Chapter I", s, 42, UiKit.Display);
        var t2 = UiKit.Text("The Hamlet", s, 30, UiKit.Display);
        t1.HorizontalAlignment = t2.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddChild(t1); title.AddChild(t2);
        title.AddChild(UiKit.Rule(s));
        col.AddChild(title);
        _title = title;

        var how = new VBoxContainer { Name = "HowTo", MouseFilter = MouseFilterEnum.Ignore };
        _howPara = Para(HowTo, 16, UiKit.Serif, UiKit.Ink);
        _howPara.Name = "HowToText";
        _keysPara = Para(Keys, 13, UiKit.UiFont, UiKit.FadedInk.Darkened(0.35f));
        _keysPara.Name = "KeysText";
        how.AddChild(_howPara);
        how.AddChild(_keysPara);
        col.AddChild(how);
        _howTo = how;

        col.AddChild(Heading("The village"));
        _day = Entry(col, "Date", out _, false);
        _walkers = Entry(col, "Walkers", out _, false);
        _seed = Entry(col, "Map", out _, false);

        if (river) BuildLedger(col);

        col.AddChild(Heading("Path health"));
        _detour = Entry(col, "Detour ratio", out _detourNote);
        _cover = Entry(col, "Trail coverage", out _coverNote);
        _trunk = Entry(col, "Trunk share", out _trunkNote);
        _gauge = new BandGauge { Name = "TrunkBand", CustomMinimumSize = new Vector2(0, UiKit.Px(s, 14)), Scale1 = s };
        col.AddChild(_gauge);

        var legend = new VBoxContainer { Name = "Legend", MouseFilter = MouseFilterEnum.Ignore };
        legend.AddThemeConstantOverride("separation", UiKit.Px(s, 3));
        _legend = legend;
        _legendHead = Heading("The ground");
        legend.AddChild(_legendHead);
        _ground = GroundLegend();
        legend.AddChild(_ground);
        _heat = new HeatBar { Name = "HeatLegend", CustomMinimumSize = new Vector2(0, UiKit.Px(s, 34)), Scale1 = s, Visible = false };
        legend.AddChild(_heat);
        col.AddChild(legend);

        col.AddChild(UiKit.Rule(s, 0.35f));
        _status = Para("", 14, UiKit.Serif, UiKit.Ink);
        _status.Name = "Status";
        col.AddChild(_status);
        _preview = Para("", 14, UiKit.UiBold, UiKit.Ink);
        _preview.Name = "PreviewLine";
        _preview.Visible = false;
        col.AddChild(_preview);
        // With the river the page holds more, so the ground legend folds away too (the overlay key W still names it),
        // and then the older margin notes.
        _optional = river ? new Control[] { _keysPara, _howPara, _legend, _notes[3], _notes[2] } : new Control[] { _keysPara, _howPara };
        Folded = 0;
    }

    private Label Para(string text, float px, FontFile font, Color c)
    {
        var l = UiKit.Text(text, _s, px, font, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(UiKit.Px(_s, 120), 0);
        return l;
    }

    private Label Heading(string text)
    {
        var l = UiKit.Text(text, _s, 20, UiKit.Serif);
        l.Name = text.Replace(" ", "");
        return l;
    }

    /// <summary>"Name ........ value" with an optional small note under it.</summary>
    private Label Entry(Container col, string name, out Label note, bool withNote = true)
    {
        var row = new HBoxContainer { Name = name.Replace(" ", ""), MouseFilter = MouseFilterEnum.Ignore };
        var n = UiKit.Text(name, _s, 16);
        n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var v = UiKit.Text("-", _s, 16, UiKit.UiBold);
        v.Name = "Value";
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.SizeFlagsStretchRatio = 2.2f;
        v.CustomMinimumSize = new Vector2(UiKit.Px(_s, 60), 0);
        row.AddChild(n); row.AddChild(v);
        col.AddChild(row);
        note = null!;
        if (!withNote) return v; // (a label built and never added would leak: an orphan CanvasItem at exit)
        note = UiKit.Text("", _s, 13, UiKit.UiFont, UiKit.FadedInk.Darkened(0.3f));
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(UiKit.Px(_s, 60), 0);
        note.Name = name.Replace(" ", "") + "Note";
        col.AddChild(note);
        return v;
    }

    private Control GroundLegend()
    {
        var grid = new GridContainer { Name = "GroundLegend", Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", UiKit.Px(_s, 14));
        grid.AddThemeConstantOverride("v_separation", UiKit.Px(_s, 3));
        (string file, string name)[] g = { ("grass", "Grass"), ("trampled", "Trampled"), ("trail", "Trail"), ("track", "Track"), ("hollow-way", "Hollow way") };
        foreach (var (file, name) in g)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
            var sw = new TextureRect
            {
                Texture = GD.Load<Texture2D>($"res://assets/textures/ground-{file}.png"),
                CustomMinimumSize = new Vector2(UiKit.Px(_s, 30), UiKit.Px(_s, 16)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Tile,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            row.AddChild(Framed(sw));
            row.AddChild(UiKit.Text(name, _s, 14));
            grid.AddChild(row);
        }
        var river = new ColorRect { Color = Palette.River, CustomMinimumSize = new Vector2(UiKit.Px(_s, 30), UiKit.Px(_s, 16)), MouseFilter = MouseFilterEnum.Ignore };
        var rrow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rrow.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        rrow.AddChild(Framed(river));
        rrow.AddChild(UiKit.Text("River", _s, 14));
        grid.AddChild(rrow);
        return grid;
    }

    private Control Framed(Control c)
    {
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var b = new StyleBoxFlat { BgColor = UiKit.Ink, DrawCenter = false, BorderColor = UiKit.Ink };
        b.SetBorderWidthAll(1);
        b.SetContentMarginAll(1);
        p.AddThemeStyleboxOverride("panel", b);
        p.AddChild(c);
        return p;
    }

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

    // ------------------------------------------------------------------ updates

    public void SetOverlay(bool on)
    {
        _overlay = on;
        _ground.Visible = !on && !Slip;
        _heat.Visible = on && !Slip;
        ((Label)_legendHead).Text = on ? "Wear" : "The ground";
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetPreviewLine(string text)
    {
        _preview.Text = text.Length > 0 ? "Typical routes: " + text + "." : "";
        _preview.Visible = text.Length > 0;
    }

    /// <summary>The dawn bell rang: the date entry says so for a few seconds (the bell's visual twin).</summary>
    public void Bell(double now) { _bellUntil = now + 4; _lastDay = -1; }

    /// <summary>Refreshes the entries whose values changed (called about twice a second).</summary>
    public void Refresh(in SimStats st, int walkersOut, int seed, double now)
    {
        bool bell = now < _bellUntil;
        if (st.Day != _lastDay || st.Season != _lastSeason || st.Year != _lastYear || bell != _bellShown)
        {
            _lastDay = st.Day; _lastSeason = st.Season; _lastYear = st.Year; _bellShown = bell;
            _day.Text = bell ? $"Dawn bell, day {st.Day}, {Seasons[st.Season & 3]}, year {st.Year}" : $"Day {st.Day}, {Seasons[st.Season & 3]}, year {st.Year}";
        }
        if (walkersOut != _lastOut || st.Walkers != _lastWalkers)
        {
            _lastOut = walkersOut; _lastWalkers = st.Walkers;
            _walkers.Text = $"{walkersOut} out of {st.Walkers}";
        }
        if (seed != _lastSeed) { _lastSeed = seed; _seed.Text = $"Seed {seed}"; }
        bool settled = st.SimTime > 30; // bands mean something once the town has worn in, as in the toy
        int d = double.IsFinite(st.DetourRatio) ? (int)System.Math.Round(st.DetourRatio * 100) : -1;
        if (d != _lastDetour)
        {
            _lastDetour = d;
            _detour.Text = d < 0 ? "-" : (d / 100.0).ToString("0.00");
            _detourNote.Text = "target 1.05 to 1.30" + Band(settled, st.DetourRatio, 1.05, 1.3);
        }
        int c = (int)System.Math.Round(st.TrailCoverage * 1000);
        if (c != _lastCover)
        {
            _lastCover = c;
            _cover.Text = (c / 10.0).ToString("0.0") + "%";
            _coverNote.Text = "hamlet 3% to 8%" + Band(settled, st.TrailCoverage, 0.03, 0.08);
        }
        int t = double.IsFinite(st.TrunkShare) ? (int)System.Math.Round(st.TrunkShare * 1000) : -1;
        if (t != _lastTrunk)
        {
            _lastTrunk = t;
            _trunk.Text = t < 0 ? "-" : (t / 10.0).ToString("0.0") + "%";
            _trunkNote.Text = "band 30% to 70%" + Band(settled, st.TrunkShare, 0.3, 0.7);
            _gauge.Value = t < 0 ? -1 : t / 1000f;
            _gauge.QueueRedraw();
        }
    }

    // In words, so the band never relies on colour alone.
    private static string Band(bool settled, double v, double lo, double hi)
        => !settled || !double.IsFinite(v) ? "" : v < lo ? " · below" : v > hi ? " · above" : " · in band";

    /// <summary>Lays the column over the visible part of the page (pageRect in screen pixels), or folds it into a slip.</summary>
    public void Place(Rect2 page, Vector2 vp)
    {
        PageRect = page;
        // Side margins in proportion to the page; top and bottom at least MinInset plus a little air, and never less
        // than the side margin's share of the height, so the last line stays clear of the book's bottom edge.
        float s = _s, margin = Mathf.Max(UiKit.Px(s, 18), page.Size.X * 0.07f);
        float vmargin = Mathf.Max(MinInset + UiKit.Px(s, 8), page.Size.Y * 0.055f);
        var inner = new Rect2(page.Position + new Vector2(margin, vmargin), page.Size - new Vector2(2 * margin, 2 * vmargin));
        var screen = new Rect2(Vector2.Zero, vp).Grow(-UiKit.Px(s, 12));
        var vis = inner.Intersection(screen);
        bool slip = vis.Size.X < 290 * s || vis.Size.Y < 520 * s;
        if (slip != Slip)
        {
            Slip = slip;
            AddThemeStyleboxOverride("panel", slip ? _slip : _onPage);
            _title.Visible = _howTo.Visible = !slip;
            _legendHead.Visible = !slip;
            if (_chronicle != null) _chronicle.Visible = !slip;
            SetOverlay(_overlay);
            MouseFilter = slip ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }
        Rect2 r;
        if (slip) r = new Rect2(UiKit.Px(s, 12), UiKit.Px(s, 12), 320 * s, 0);
        else
        {
            float w = Mathf.Min(vis.Size.X, 540 * s);
            float x = Mathf.Clamp(inner.Position.X + (inner.Size.X - w) / 2f, vis.Position.X, vis.End.X - w);
            r = new Rect2(x, vis.Position.Y, w, vis.Size.Y);
        }
        if (!slip) Fit(r.Size.Y);
        if (r == _placed) return;
        _placed = r;
        CustomMinimumSize = new Vector2(r.Size.X, 0); // first, or a wider old minimum would stop the shrink
        Position = r.Position;
        Size = new Vector2(r.Size.X, slip ? 0 : r.Size.Y);
    }

    /// <summary>
    /// Folds the optional parts away one at a time while the column is taller than the room on the page, and unfolds
    /// them when there is room again (with a few pixels of hysteresis, so it never flickers). One step per frame;
    /// allocation-free.
    /// </summary>
    private void Fit(float avail)
    {
        float need = _col.GetCombinedMinimumSize().Y;
        if (need > avail && Folded < _optional.Length)
        {
            var c = _optional[Folded];
            _optionalH[Folded] = c.Size.Y + _col.GetThemeConstant("separation");
            c.Visible = false;
            Folded++;
        }
        else if (Folded > 0 && need + _optionalH[Folded - 1] + UiKit.Px(_s, 6) <= avail)
        {
            Folded--;
            var c = _optional[Folded];
            c.Visible = c is not Label { Text.Length: 0 }; // an empty margin note stays hidden
        }
    }

    /// <summary>The trunk share on a 0 to 100% rule with the 30% to 70% band hatched and a diamond at the value.</summary>
    public partial class BandGauge : Control
    {
        public float Value = -1, Scale1 = 1;
        public override void _Draw()
        {
            float w = Size.X, h = Size.Y, y = h / 2f, s = Scale1;
            DrawLine(new Vector2(0, y), new Vector2(w, y), UiKit.FadedInk, Mathf.Max(1, 1.5f * s));
            var band = new Rect2(w * 0.3f, y - 4 * s, w * 0.4f, 8 * s);
            DrawRect(band, new Color(UiKit.Good, 0.25f));
            DrawRect(band, UiKit.Ink, false, Mathf.Max(1, s));
            if (Value < 0) return;
            float x = Mathf.Clamp(Value, 0, 1) * w, r = 6 * s;
            var pts = new[] { new Vector2(x, y - r), new Vector2(x + r, y), new Vector2(x, y + r), new Vector2(x - r, y) };
            DrawColoredPolygon(pts, UiKit.Ink);
        }
    }

    /// <summary>The wear heat legend: the overlay's colour ramp with the toy's stops 0, 10, 25, 50 and 85+.</summary>
    public partial class HeatBar : Control
    {
        public float Scale1 = 1;
        private static readonly Color[] Stops =
        {
            new(0.949f, 0.925f, 0.796f), new(0.941f, 0.765f, 0.353f), new(0.878f, 0.455f, 0.173f), new(0.690f, 0.165f, 0.180f), new(0.298f, 0.063f, 0.212f),
        };
        private static readonly float[] At = { 0, 10, 25, 50, 85 };
        private static readonly string[] Labels = { "0", "10", "25", "50", "85+" };

        public override void _Draw()
        {
            float w = Size.X, s = Scale1, h = 12 * s;
            const int steps = 60;
            for (int i = 0; i < steps; i++)
            {
                float v = 85f * i / steps;
                int k = 0;
                while (k < 3 && v >= At[k + 1]) k++;
                var c = Stops[k].Lerp(Stops[k + 1], (v - At[k]) / (At[k + 1] - At[k]));
                DrawRect(new Rect2(w * i / steps, 0, w / steps + 1, h), c);
            }
            DrawRect(new Rect2(0, 0, w, h), UiKit.Ink, false, Mathf.Max(1, s));
            var font = UiKit.UiFont;
            int fs = UiKit.FontPx(s, 12);
            for (int i = 0; i < 5; i++)
            {
                float x = w * At[i] / 85f;
                DrawLine(new Vector2(x, h), new Vector2(x, h + 4 * s), UiKit.Ink, Mathf.Max(1, s));
                float tw = font.GetStringSize(Labels[i], HorizontalAlignment.Left, -1, fs).X;
                DrawString(font, new Vector2(Mathf.Clamp(x - tw / 2, 0, w - tw), h + 6 * s + fs), Labels[i], HorizontalAlignment.Left, -1, fs, UiKit.Ink);
            }
        }
    }
}
