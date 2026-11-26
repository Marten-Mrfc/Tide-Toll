using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The town's interface in the book's style (UiKit: paper cards with an ink edge, Atkinson Hyperlegible, Alegreya for
/// headings): the ledger card (coin, coin a minute, the level bar with renown and the next gift), margin notes for
/// events, a floating "+N" at every delivery, and (TownHud.Shop.cs) the upgrade bookmarks, the building offers, the
/// Pave mode and the bell. Every control shows its key. Built for the window's height (UiKit.ScaleFor), so it reads
/// at 1280 x 720 and 1920 x 1080; rebuilt when the window changes size.
/// </summary>
public partial class TownHud : CanvasLayer
{
    public const int MaxNotes = 3, MaxFloaters = 32;
    public const float NoteSeconds = 7f, FloatSeconds = 1.8f, BannerSeconds = 3.5f; // round 6: banners 6 s to 3.5 s

    private Main _main = null!;
    private TownDriver _drv = null!;
    private float _s = 1f;
    private Vector2 _built;
    private Control _root = null!;
    private Label _coin = null!, _rate = null!, _level = null!, _renown = null!, _next = null!;
    private ColorRect _barFill = null!;
    private Control _barBack = null!;
    private VBoxContainer _notes = null!;
    private readonly List<(Label label, float age)> _noteList = new();
    private Control _floatLayer = null!;
    private Control? _ledger;
    private readonly Label[] _floaters = new Label[MaxFloaters];
    private readonly Vector3[] _floatAt = new Vector3[MaxFloaters];
    private readonly float[] _floatAge = new float[MaxFloaters];
    private readonly double[] _floatCoin = new double[MaxFloaters];
    private readonly Vector2[] _floatCell = new Vector2[MaxFloaters];
    private int _floatNext;

    /// <summary>Notes shown so far (tests), the text of the newest, floaters started.</summary>
    public int NotesShown { get; private set; }
    public string LastNote { get; private set; } = "";
    public readonly List<string> NoteLog = new();
    public int FloatersStarted { get; private set; }
    public string CoinText => _coin.Text;
    public string NextText => _next.Text;
    public float UiScale => _s;
    /// <summary>The small pause control (bottom right): closes the book like Esc.</summary>
    public Button PauseMark { get; private set; } = null!;

    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Money(double c) => Math.Floor(c).ToString("N0", Inv);

    public void Init(Main main)
    {
        _main = main;
        _drv = main.Driver;
        _drv.Replaced += OnTownReplaced;
        Name = "Hud";
        Layer = 5;
        Build();
    }

    public static Texture2D? IconOr(string name, string standIn)
    {
        foreach (var n in new[] { name, standIn })
        {
            string p = $"res://assets/icons/{n}.svg";
            if (ResourceLoader.Exists(p)) return ModelLib.MipTexture(p);
        }
        return null;
    }

    private void Build()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        var vp = GetViewport().GetVisibleRect().Size;
        _built = vp;
        // The interface size setting is the window's content scale (applied by the settings); build in canvas units from
        // the window's real height, so the size is not counted twice.
        _s = UiKit.ScaleFor(GetWindow().Size.Y);
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.MakeTheme(_s) };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _floatLayer = new Control { Name = "Floaters", MouseFilter = Control.MouseFilterEnum.Ignore };
        _floatLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_floatLayer);
        for (int i = 0; i < MaxFloaters; i++)
        {
            // Round 3: gold with a dark edge, a little bigger, rising and fading.
            // Round 6: bigger (34), one number per delivery spot (see Float), lifted clear of the building's roof.
            var l = UiKit.Text("", _s, 34, UiKit.UiBold, Color.FromHtml("#F2C14E"));
            l.AddThemeColorOverride("font_outline_color", Color.FromHtml("#3A2A10"));
            l.AddThemeConstantOverride("outline_size", UiKit.Px(_s, 6));
            l.Visible = false;
            _floatLayer.AddChild(l);
            _floaters[i] = l;
        }

        BuildLedger();
        BuildRequestCard();
        BuildBanner();
        BuildHomeMark();
        _notes = new VBoxContainer { Name = "Notes", MouseFilter = Control.MouseFilterEnum.Ignore };
        _notes.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        _notes.Position = new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 200));
        _notes.Size = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(_notes);
        _noteList.Clear();
        BuildShop();
        BuildPave();
        BuildSlim();
        PauseMark = new Button { Name = "PauseMark", Text = "Pause  [Esc]", FocusMode = Control.FocusModeEnum.None, TooltipText = "Close the book: the menu" };
        _root.AddChild(PauseMark);
        PauseMark.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, UiKit.Px(_s, 16));
        PauseMark.GrowHorizontal = Control.GrowDirection.Begin;
        PauseMark.GrowVertical = Control.GrowDirection.Begin;
        PauseMark.Pressed += () => _main.Menu.Pause();
        // Round 4: a whole card (the bookmark style has no left edge, which read as clipped), placed every frame.
        foreach (var (state, bg, w) in new[] { ("normal", UiKit.Paper, 1.5f), ("hover", UiKit.PaperHover, 1.5f), ("pressed", UiKit.Pressed, 2f), ("hover_pressed", UiKit.Pressed, 2f) })
        {
            var box = UiKit.Card(_s, bg, w);
            box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(_s, 12);
            box.ContentMarginTop = box.ContentMarginBottom = UiKit.Px(_s, 5);
            PauseMark.AddThemeStyleboxOverride(state, box);
        }
        BuildBookParts();
        BuildRiverParts();
        BuildChoiceParts();
        _fullParts = new Control?[] { _ledger, _notes, _shop, _bell, _nextAge, _floatLayer, _banner, _achMark };
        _fullA = -1f;
        _labelCards = Array.Empty<Control?>(); // round 6: the cards the map's labels keep clear of, taken again after a rebuild
        Refresh();
    }

    private void BuildLedger()
    {
        var card = new PanelContainer { Name = "Ledger", MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.96f), 1.5f);
        box.SetContentMarginAll(UiKit.Px(_s, 10));
        card.AddThemeStyleboxOverride("panel", box);
        card.Position = new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 14));
        card.CustomMinimumSize = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(card);
        _ledger = card;
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 3));
        card.AddChild(col);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiKit.Px(_s, 8));
        col.AddChild(row);
        var coinIcon = IconOr("ui-coin", "ui-coin");
        if (coinIcon != null)
            row.AddChild(new TextureRect
            {
                Texture = coinIcon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = Vector2.One * UiKit.Px(_s, 30), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        _coin = UiKit.Text("0 coin", _s, 26, UiKit.UiBold);
        row.AddChild(_coin);
        _rate = UiKit.Text("", _s, 16, UiKit.UiFont, UiKit.FadedInk);
        _rate.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        row.AddChild(_rate);
        col.AddChild(UiKit.Rule(_s, 0.5f));
        _level = UiKit.Text("Level 1", _s, 19, UiKit.Serif);
        _level.AddThemeConstantOverride("line_spacing", 0);
        col.AddChild(_level);
        _barBack = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 12)) };
        var back = UiKit.Card(_s, UiKit.PaperDark, 1f);
        back.SetContentMarginAll(0);
        _barBack.AddThemeStyleboxOverride("panel", back);
        col.AddChild(_barBack);
        _barFill = new ColorRect { Color = Palette.Thatch, MouseFilter = Control.MouseFilterEnum.Ignore };
        _barBack.AddChild(_barFill);
        _renown = UiKit.Text("", _s, 15, UiKit.UiFont, UiKit.FadedInk);
        col.AddChild(_renown);
        _next = UiKit.Text("", _s, 17, UiKit.UiFont);
        _next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _next.CustomMinimumSize = new Vector2(UiKit.Px(_s, 310), 0);
        col.AddChild(_next);
        BuildHintLine(col);
    }

    public override void _Process(double delta)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp != _built && vp.Y > 1) Build();
        foreach (var e in _drv.Events) OnEvent(e);
        StageFrame((float)delta);
        Refresh();
        StepNotes((float)delta);
        StepFloaters((float)delta);
        ShopFrame((float)delta);
        PaveFrame();
        RequestFrame((float)delta);
        HintFrame((float)delta);
        BannerFrame((float)delta);
        HomeFrame();
        BookFrame((float)delta);
        RiverFrame((float)delta);
        ChoiceFrame((float)delta);
        ColumnFrame();
        LabelsFrame((float)delta);
    }

    /// <summary>
    /// Round 6: the left column (ledger, request or river request, drive, notes) is stacked once a frame, last, from the
    /// cards' minimum sizes, so a hint that wraps to more lines or a card that grows this frame never lies under the next
    /// card (the hint line was cut by the request card). The ledger shrinks back when its hint goes.
    /// </summary>
    private void ColumnFrame()
    {
        if (_ledger == null) return;
        float gap = UiKit.Px(_s, 8);
        _ledger.ResetSize();
        float y = _ledger.Position.Y + Math.Max(_ledger.Size.Y, _ledger.GetCombinedMinimumSize().Y) + gap;
        for (int k = 0; k < 2; k++)
        {
            Control? c = k == 0 ? _req : _drive;
            if (c == null || !IsInstanceValid(c) || !c.Visible) continue;
            c.ResetSize();
            c.Position = new Vector2(c.Position.X, y);
            y += Math.Max(c.Size.Y, c.GetCombinedMinimumSize().Y) + gap;
        }
        _notes.Position = new Vector2(UiKit.Px(_s, 16), y + UiKit.Px(_s, 2));
        ColumnBottom = y;
    }

    /// <summary>Where the left column's cards end (tests: nothing in the column overlaps).</summary>
    public float ColumnBottom { get; private set; }
    public Rect2 LedgerRect => _ledger?.GetGlobalRect() ?? new Rect2();
    public Rect2 RequestRect => _req.Visible ? _req.GetGlobalRect() : new Rect2();

    // Last values shown, so text is rebuilt only when a value changes (round 2: no strings built per frame).
    private long _shownCoin = -1, _shownRate = -1, _shownRenown = -1, _shownNextRenown = -1;
    private int _shownLevel = -1;
    private bool _shownRung;

    private void Refresh()
    {
        var t = _drv.Town;
        long coin = (long)Math.Floor(t.Coin), rate = (long)Math.Floor(t.CoinPerMinute);
        bool coinChanged = coin != _shownCoin;
        if (coinChanged) { _shownCoin = coin; _coin.Text = Money(coin) + " coin"; }
        if (rate != _shownRate) { _shownRate = rate; _rate.Text = Money(rate) + " a minute"; }
        bool levelChanged = t.Level != _shownLevel || t.NextAgeRung != _shownRung;
        if (levelChanged)
        {
            _shownLevel = t.Level; _shownRung = t.NextAgeRung;
            _level.Text = t.Level >= t.MaxLevel ? $"Level {t.Level}, the last" : $"Level {t.Level} of {t.MaxLevel}";
            _next.Text = t.NextGift;
        }
        float bar = (float)Math.Clamp(t.LevelBar, 0, 1);
        var sz = _barBack.Size;
        _barFill.Position = new Vector2(1, 1);
        _barFill.Size = new Vector2(Math.Max(0, (sz.X - 2) * bar), Math.Max(0, sz.Y - 2));
        long renown = (long)Math.Floor(t.Renown), nextRenown = t.Level >= t.MaxLevel ? -1 : (long)Math.Floor(t.NextLevelRenown);
        if (renown != _shownRenown || nextRenown != _shownNextRenown)
        {
            _shownRenown = renown; _shownNextRenown = nextRenown;
            _renown.Text = nextRenown < 0 ? $"Renown {Money(renown)}" : $"Renown {Money(renown)} of {Money(nextRenown)}";
        }
        if (coinChanged || levelChanged) _slimText.Text = $"{Money(coin)} coin   Level {t.Level} of {t.MaxLevel}";
    }

    // ------------------------------------------------------------------ zoom stages

    private PanelContainer _slim = null!;
    private Label _slimText = null!;
    private float _fullA = 1f;
    private bool _scrollHintShown;
    /// <summary>Opacity of the full interface (1 zoomed in, 0 once the page is in view) and whether the slim strip shows.</summary>
    public float FullAlpha => _fullA;
    public bool SlimVisible => _slim.Visible;
    public string SlimText => _slimText.Text;
    /// <summary>Game seconds after which the one-time hint to scroll out appears, if the player has not zoomed out yet.</summary>
    public const double ScrollHintAt = 75;
    public const string ScrollHint = "Scroll out to see the whole book.";

    private void BuildSlim()
    {
        _slim = new PanelContainer { Name = "Slim", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.94f), 1.2f);
        box.SetContentMarginAll(UiKit.Px(_s, 6));
        box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(_s, 14);
        _slim.AddThemeStyleboxOverride("panel", box);
        _slimText = UiKit.Text("", _s, 18, UiKit.UiBold);
        _slim.AddChild(_slimText);
        _root.AddChild(_slim);
        _shownCoin = -1; _shownRate = -1; _shownRenown = -1; _shownNextRenown = -1; _shownLevel = -1;
    }

    /// <summary>
    /// The interface follows the zoom: complete while zoomed in on the town; as the whole page comes into view it fades
    /// to a slim strip (coin and level) at the top; with the book in view the left page carries the summary.
    /// </summary>
    private void StageFrame(float dt)
    {
        var rig = _main.Rig;
        // Round 3: the full interface is gone a little before the slim stage, so it never overlaps the left page.
        float size = rig.Camera.Size, slim = _main.SlimSize, fade = slim * 0.12f, gone = slim * 0.94f;
        float a = Math.Clamp((gone - size) / fade, 0f, 1f);
        if (a != _fullA)
        {
            _fullA = a;
            var m = new Color(1, 1, 1, a);
            bool vis = a > 0.01f;
            foreach (var c in _fullParts) { if (c == null || !IsInstanceValid(c)) continue; c.Modulate = m; c.Visible = vis; }
        }
        var stage = _main.Stage;
        if (stage != _lastStage)
        {
            // Turning to the whole book (and back) sounds like a page.
            if (_lastStage != (Main.ZoomStage)(-1) && (stage == Main.ZoomStage.Book || _lastStage == Main.ZoomStage.Book)) _main.Audio.PageTurn();
            _lastStage = stage;
        }
        _slim.Visible = a < 1f;
        if (_slim.Visible)
        {
            _slim.Modulate = new Color(1, 1, 1, 1f - a);
            _slim.ResetSize();
            // Round 6: bottom left, over the left page's blank foot (in the top middle it lay over the map's title, top
            // left over the town's name).
            _slim.Position = new Vector2(UiKit.Px(_s, 16), _built.Y - _slim.Size.Y - UiKit.Px(_s, 12));
        }
        if (!_scrollHintShown && size >= slim) _scrollHintShown = true; // found it alone: no hint needed
        if (!_scrollHintShown && _drv.Town.Time >= ScrollHintAt) { _scrollHintShown = true; Note(ScrollHint); }
    }

    private Control?[] _fullParts = Array.Empty<Control?>();
    private Main.ZoomStage _lastStage = (Main.ZoomStage)(-1);

    /// <summary>
    /// Round 4: true when a screen point (canvas units) is on a card of the interface: the ledger, the request card, the
    /// hint line, the margin notes, the level banner, the shop column, the bell, the slim strip, the mode banner and the
    /// small controls. Pave and Extend find nothing there and the camera starts no drag there, so nothing behind a card
    /// is paved, extended or dragged through it.
    /// </summary>
    public bool OverInterface(Vector2 p)
    {
        if (!Visible) return false;
        if (Over(_ledger, p) || Over(_req, p) || Over(_hint, p) || Over(_banner, p) || Over(_shop, p) || Over(_bell, p)
            || Over(_nextAge, p) || Over(_slim, p) || Over(_paveBanner, p) || Over(_achMark, p) || Over(PauseMark, p) || Over(_home, p)
            || Over(_choice, p) || Over(_drive, p))
            return true;
        for (int i = 0; i < _noteList.Count; i++)
            if (IsInstanceValid(_noteList[i].label) && Over(_noteList[i].label.GetParent() as Control, p)) return true;
        return false;
    }

    private static bool Over(Control? c, Vector2 p)
        => c != null && IsInstanceValid(c) && c.IsVisibleInTree() && c.Modulate.A > 0.05f && c.GetGlobalRect().HasPoint(p);

    // ------------------------------------------------------------------ notes

    /// <summary>A margin note: a slip of paper on the left that fades after a few seconds.</summary>
    public void Note(string text, bool strong = false)
    {
        // The same note again while it is still on the page: keep the one slip and give it its full time again.
        for (int i = 0; i < _noteList.Count; i++)
            if (IsInstanceValid(_noteList[i].label) && _noteList[i].label.Text == text) { _noteList[i] = (_noteList[i].label, 0f); NotesShown++; LastNote = text; Log(NoteLog, text); return; }
        var l = UiKit.Text(text, _s, strong ? 18 : 16, strong ? UiKit.UiBold : UiKit.UiFont);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(UiKit.Px(_s, 320), 0);
        var slip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Card(_s, new Color(strong ? UiKit.Pressed : UiKit.Paper, 0.95f), strong ? 2f : 1f);
        box.SetContentMarginAll(UiKit.Px(_s, 7));
        slip.AddThemeStyleboxOverride("panel", box);
        slip.AddChild(l);
        _notes.AddChild(slip);
        _notes.MoveChild(slip, 0);
        _noteList.Add((l, 0f));
        while (_notes.GetChildCount() > MaxNotes)
        {
            var old = _notes.GetChild(_notes.GetChildCount() - 1);
            _notes.RemoveChild(old);
            old.QueueFree();
        }
        NotesShown++;
        LastNote = text;
        Log(NoteLog, text);
    }

    private void StepNotes(float dt)
    {
        float top = _req.Visible ? _req.GetRect().End.Y : _ledger != null ? _ledger.GetRect().End.Y : 180;
        if (_drive != null && _drive.Visible) top = Math.Max(top, _drive.GetRect().End.Y);
        _notes.Position = new Vector2(UiKit.Px(_s, 16), top + UiKit.Px(_s, 10));
        for (int i = _noteList.Count - 1; i >= 0; i--)
        {
            var (l, age) = _noteList[i];
            age += dt;
            if (!IsInstanceValid(l) || age > NoteSeconds)
            {
                if (IsInstanceValid(l) && l.GetParent() is Control slip && IsInstanceValid(slip)) { slip.GetParent()?.RemoveChild(slip); slip.QueueFree(); }
                _noteList.RemoveAt(i);
                continue;
            }
            _noteList[i] = (l, age);
            if (l.GetParent() is Control p) p.Modulate = new Color(1, 1, 1, Math.Clamp((NoteSeconds - age) / 1.2f, 0f, 1f));
        }
    }

    private void OnEvent(in TownEvent e)
    {
        var t = _drv.Town;
        switch (e.Kind)
        {
            case TownEventKind.Delivery: if (Settings.GameSettings.Current?.ShowCoinNumbers ?? true) Float(e.X, e.Y, e.Coin); break;
            case TownEventKind.LevelReached:
                if (e.Level > 1) { Banner($"Level {e.Level}", $"{Upper(e.Text)}."); BannerClearOf(GiftPoint(e.Level)); }
                break;
            case TownEventKind.RequestAsked: case TownEventKind.RequestMet: case TownEventKind.RequestMissed: RequestEvent(e); break;
            case TownEventKind.CottageExtended: Note(e.Level >= 2 ? "A cottage is extended again: room for one more." : "A cottage is extended: room for one more."); break;
            case TownEventKind.BuildingOpened: Note((e.Text ?? "A new building") + " is open.", true); break;
            case TownEventKind.OfferAvailable: Note("New to buy: " + Upper(e.Text ?? "a building") + "."); break;
            case TownEventKind.RoadWornIn: Note("A road has worn in. It can be paved now."); break;
            case TownEventKind.RoadPaved: Note(e.Surface == Surface.Cobbles ? "A road is cobbled." : "A road is laid with gravel."); break;
            case TownEventKind.PerkEarned: if (_perksSeen.Add(e.Text ?? "")) Note("Perk: " + (e.Text ?? "") + "."); break;
            case TownEventKind.AchievementUnlocked: Note("Achievement: " + (e.Text ?? "") + "."); break;
            case TownEventKind.VillagerArrived: if (t.VillagerCount > 1) Note("A new villager moves in."); break;
            case TownEventKind.Charter:
                Banner("The charter", "The town is granted its charter. Ring in the next age when you are ready.");
                FocusBellGreen();
                break;
            default: if (!ChoiceEvent(e)) RiverEvent(e); break;
        }
    }

    private readonly HashSet<string> _perksSeen = new();
    /// <summary>Perk notes shown in this town so far (tests: a new town starts at 0).</summary>
    public int PerksNoted => _perksSeen.Count;
    /// <summary>A request card's Done or Time ran out flash is showing (tests).</summary>
    public bool RequestFlashing => _reqFlash != 0;

    /// <summary>The note and banner logs keep the last <see cref="LogCap"/> lines (round 4: they grew without limit).</summary>
    public const int LogCap = 200;
    private static void Log(List<string> log, string line)
    {
        log.Add(line);
        if (log.Count > LogCap) log.RemoveAt(0);
    }

    /// <summary>
    /// Round 4: a new town (New town, Delete, a load) starts clean: perk notes may show again, and the old town's hint
    /// marker and request flash are dropped at once.
    /// </summary>
    private void OnTownReplaced()
    {
        _perksSeen.Clear();
        _hintShown = ""; _hintAge = 0; _hintRoad = -1; _hintPlot = -1; _reqFlash = 0;
        if (_marker != null && IsInstanceValid(_marker)) _marker.Visible = false;
        if (_hint != null && IsInstanceValid(_hint)) _hint.Text = "";
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= OnTownReplaced; }

    private static string Upper(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "Something new";
        return char.ToUpperInvariant(s[0]) + s.Substring(1).TrimEnd('.');
    }

    // ------------------------------------------------------------------ floating coin

    /// <summary>Coin numbers on screen now (tests: one number per spot, none while a mode points at something).</summary>
    public int FloatersVisible { get { int n = 0; for (int i = 0; i < MaxFloaters; i++) if (_floatAge[i] > 0 && _floaters[i].Visible) n++; return n; } }

    private void Float(double x, double y, double coin)
    {
        // Round 6: a delivery where a number is still rising adds to it (it was drawn twice, a faded copy above).
        var cell = new Vector2((float)x, (float)y);
        for (int j = 0; j < MaxFloaters; j++)
        {
            if (_floatAge[j] <= 0 || _floatCell[j].DistanceSquaredTo(cell) > 4f) continue; // any number still showing there
            _floatCoin[j] += coin;
            _floatAge[j] = Math.Min(_floatAge[j], 0.3f); // drops back a little and shows full again, no jump to the start
            _floaters[j].Text = "+" + Money(Math.Max(1, _floatCoin[j]));
            return;
        }
        int i = _floatNext;
        _floatNext = (_floatNext + 1) % MaxFloaters;
        _floatCell[i] = cell;
        _floatCoin[i] = coin;
        // Round 6: anchored above the building (higher and a little north), so it clears the door and the people there.
        _floatAt[i] = new Vector3((float)x * TownGround.Cell, 9f, ((float)y - 1.5f) * TownGround.Cell);
        _floatAge[i] = 0.0001f;
        _floaters[i].Text = "+" + Money(Math.Max(1, coin));
        _floaters[i].Visible = true;
        FloatersStarted++;
    }

    private void StepFloaters(float dt)
    {
        for (int i = 0; i < MaxFloaters; i++)
        {
            if (_floatAge[i] <= 0) continue;
            _floatAge[i] += dt;
            var l = _floaters[i];
            if (_floatAge[i] > FloatSeconds) { _floatAge[i] = 0; l.Visible = false; continue; }
            // Round 6: hidden while Pave or Extend points at something (they lay over the lit cottage and the tip).
            bool modeOn = PaveMode || ExtendMode;
            if (l.Visible == modeOn) l.Visible = !modeOn;
            if (modeOn) continue;
            var at = _main.Rig.LocalToScreen(_floatAt[i]);
            float k = _floatAge[i] / FloatSeconds;
            l.Position = at - new Vector2(l.Size.X / 2f, UiKit.Px(_s, 44) + k * UiKit.Px(_s, 36));
            l.Modulate = new Color(1, 1, 1, Math.Clamp((1f - k) * 2f, 0f, 1f));
        }
    }
}
