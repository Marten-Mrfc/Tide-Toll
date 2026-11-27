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
    public const float NoteSeconds = 7f, FloatSeconds = 1.4f, BannerSeconds = 3.5f; // round 6: banners 6 s to 3.5 s
    /// <summary>Night 3, L2: coin numbers merge within this many seconds at one building; at most this many show.</summary>
    public const float FloatMerge = 0.5f;
    public const int FloatCap = 8;
    private readonly MessageFader _floatModeF = new(0.15f, true);

    private Main _main = null!;
    private TownDriver _drv = null!;
    private float _s = 1f;
    private Vector2 _built;
    private Control _root = null!;
    private Label _coin = null!, _rate = null!, _level = null!, _renown = null!, _next = null!;
    private ColorRect _barFill = null!;
    private Control _barBack = null!;
    private Control _floatLayer = null!;
    private Control? _ledger;
    private readonly Label[] _floaters = new Label[MaxFloaters];
    private readonly Vector3[] _floatAt = new Vector3[MaxFloaters];
    private readonly float[] _floatAge = new float[MaxFloaters];
    private readonly double[] _floatCoin = new double[MaxFloaters];
    private readonly Vector2[] _floatCell = new Vector2[MaxFloaters];
    /// <summary>L2 review: a number's sideways offset on screen, chosen once when it starts, so that it never lies on a
    /// live one (two neighbouring buildings' numbers were drawn on top of each other).</summary>
    private readonly float[] _floatDx = new float[MaxFloaters];
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

    private ColorRect _scrim = null!;
    private Vector2? _forcedCanvas;
    private float _forcedScale = 1f;

    /// <summary>
    /// Tests (game-layout): build the interface for this canvas (canvas pixels) and scale instead of the window's, so
    /// every canvas of the layout sheet can be checked in one run; null builds for the window again.
    /// </summary>
    public void ForceCanvas(Vector2? canvas, float s = 1f)
    {
        _forcedCanvas = canvas;
        _forcedScale = s;
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
        var vp = _forcedCanvas ?? GetViewport().GetVisibleRect().Size;
        _built = vp;
        // The interface size setting is the window's content scale (applied by the settings); build in canvas units from
        // the window's real height, so the size is not counted twice.
        _s = _forcedCanvas != null ? _forcedScale : UiKit.ScaleFor(GetWindow().Size.Y);
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.MakeTheme(_s), Modulate = new Color(1, 1, 1, _fade) };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _floatLayer = new Control { Name = "Floaters", MouseFilter = Control.MouseFilterEnum.Ignore };
        _floatLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_floatLayer);
        for (int i = 0; i < MaxFloaters; i++)
        {
            // Round 3: gold with a dark edge, a little bigger, rising and fading.
            // Round 6: bigger (34), one number per delivery spot (see Float), lifted clear of the building's roof.
            // Night 3 (sheet section 5): 20, bold, wheat with an ink edge.
            var l = UiKit.Text("", _s, UiKit.CoinNumber, UiKit.UiBold, UiKit.CoinGold);
            l.AddThemeFontSizeOverride("font_size", UiKit.Type(_s, UiKit.CoinNumber));
            l.AddThemeColorOverride("font_outline_color", UiKit.CoinEdge);
            l.AddThemeConstantOverride("outline_size", Math.Max(4, UiKit.U(_s, 6)));
            l.Visible = false;
            _floatLayer.AddChild(l);
            _floaters[i] = l;
        }

        // A page dims the map only: the scrim lies under every zone, so the rails stay lit and usable.
        _scrim = new ColorRect { Name = "Scrim", Color = new Color(UiKit.Ink, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_scrim);
        BuildLedger();
        BuildRequestCard();
        BuildBanner();
        BuildShop();
        BuildPave();
        BuildSlim();
        BuildLine();
        BuildBookParts();
        BuildRiverParts();
        BuildChoiceParts();
        BuildSecondRequest();
        BuildNav();
        BuildTip();
        WireMessageClicks();
        _fullParts = new Control?[] { _ledgerPanel, _shop, _bell, _floatLayer };
        _fullA = -1f;
        _labelCards = Array.Empty<Control?>(); // round 6: the cards the map's labels keep clear of, taken again after a rebuild
        Refresh();
    }

    public override void _Process(double delta)
    {
        // Frame strips hold the messages' clocks: every message motion reads this frame's step (TownHud.Messages.cs).
        if (StripStep >= 0f) { delta = StripNext; StripNext = 0f; }
        _msgDt = (float)delta;
        var vp = _forcedCanvas ?? GetViewport().GetVisibleRect().Size;
        if (vp != _built && vp.Y > 1) Build();
        foreach (var e in _drv.Events) OnEvent(e);
        StageFrame((float)delta);
        Refresh();
        StepFloaters((float)delta);
        ShopFrame((float)delta);
        PaveFrame();
        ModeFadeFrame((float)delta);
        RequestFrame((float)delta);
        HintFrame((float)delta);
        TimersFrame();
        BookFrame((float)delta);
        RiverFrame((float)delta);
        ChoiceFrame((float)delta);
        SecondRequestFrame();
        ZoneFrame();
        MessagesFrame((float)delta);
        LabelsFrame((float)delta);
    }

    // ------------------------------------------------------------------ zoom stages

    private PanelContainer _slim = null!;
    private Label _slimText = null!;
    private float _fullA = 1f;
    /// <summary>Opacity of the full interface (1 zoomed in, 0 once the page is in view) and whether the slim strip shows.</summary>
    public float FullAlpha => _fullA;
    public bool SlimVisible => _slim.Visible;
    public string SlimText => _slimText.Text;
    /// <summary>Game seconds after which the one-time hint to scroll out appears, if the player has not zoomed out yet.</summary>
    public const double ScrollHintAt = 75;

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
            // Not while the book closes or opens for the pause menu: that motion has its own sounds.
            if (_lastStage != (Main.ZoomStage)(-1) && (stage == Main.ZoomStage.Book || _lastStage == Main.ZoomStage.Book) && (_main.Menu == null || !_main.Menu.IsPaused)) _main.Audio.PageTurn();
            _lastStage = stage;
        }
        // Night 3: zoomed out the purse stays as one line in its place; with the book in view only navigation stays.
        _slim.Visible = a < 1f && stage != Main.ZoomStage.Book;
        if (_slim.Visible)
        {
            // Night 3: the purse stays as one line in its own place.
            _slim.Modulate = new Color(1, 1, 1, 1f - a);
        }
        FirstMinuteFrame(dt, size, slim);
    }

    private Control?[] _fullParts = Array.Empty<Control?>();
    private float _fade = 1f;

    /// <summary>
    /// The whole interface's opacity, 0..1: PauseMenu fades it out as the book closes and in as it opens (BookMotion),
    /// rather than switching it off in one frame.
    /// </summary>
    public float Fade
    {
        get => _fade;
        set
        {
            if (value == _fade) return;
            _fade = value;
            if (_root != null && IsInstanceValid(_root)) _root.Modulate = new Color(1, 1, 1, value);
        }
    }
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
        if (Over(_ledger, p) || Over(_lineCard, p) || Over(_banner, p) || Over(_shop, p) || Over(_bell, p)
            || Over(_slim, p) || Over(_achMark, p) || Over(PauseMark, p) || Over(_home, p) || Over(_agePageMark, p)
            || Over(_choice, p) || Over(_tide, p))
            return true;
        return false;
    }

    private static bool Over(Control? c, Vector2 p)
        => c != null && IsInstanceValid(c) && c.IsVisibleInTree() && c.Modulate.A > 0.05f && c.GetGlobalRect().HasPoint(p);

    // ------------------------------------------------------------------ notes

    private void OnEvent(in TownEvent e)
    {
        var t = _drv.Town;
        FirstEvent(e);
        if (e.Kind is TownEventKind.LevelReached or TownEventKind.BuildingOpened or TownEventKind.BoatArrived or TownEventKind.BoatLeft) TagsChanged();
        switch (e.Kind)
        {
            case TownEventKind.Delivery: if (Settings.GameSettings.Current?.ShowCoinNumbers ?? true) Float(e.X, e.Y, e.Coin); break;
            case TownEventKind.LevelReached:
                if (e.Level > 1)
                {
                    // A level that opens a choice: the choice opens at once with the level in its heading and the gift
                    // is said as a note (a decision is never delayed by a banner).
                    if (t.ChoiceOpen && t.Choice.Level == e.Level) Note($"Level {e.Level}: {Upper(e.Text)}.", true, NoteBuilding, "");
                    else { Large($"Level {e.Level}", $"{Upper(e.Text)}.", PrioLevel, BannerStay, "level-reached"); BannerClearOf(GiftPoint(e.Level)); }
                }
                break;
            case TownEventKind.RequestAsked: case TownEventKind.RequestMet: case TownEventKind.RequestMissed: RequestEvent(e); break;
            // Message-system section 2: what the player did or can already see raises no note (the thing shows it).
            case TownEventKind.CottageExtended: case TownEventKind.RoadPaved: case TownEventKind.VillagerArrived: break;
            case TownEventKind.OfferAvailable: break; // the new row fades into the shop and pulses once
            case TownEventKind.RoadWornIn: PavePillPulse(); break; // the Pave pill changes to its price and pulses
            case TownEventKind.BuildingOpened: Note((e.Text ?? "A new building") + " is open.", true, NoteBuilding, "gift-arrives"); break;
            case TownEventKind.PerkEarned: if (_perksSeen.Add(e.Text ?? "")) Note("Perk: " + (e.Text ?? "") + "."); break;
            case TownEventKind.AchievementUnlocked: Note("Achievement: " + (e.Text ?? "") + ".", false, NoteAchievement, "achievement"); break;
            case TownEventKind.Charter:
                Large("The charter", "The town is granted its charter. Ring in the next age when you are ready.", PrioMoment, BannerLongStay, "charter-granted");
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
        ClearMessages();
        _hintShown = ""; _hintAge = 0; _hintRoad = -1; _hintPlot = -1; _reqFlash = 0;
        _markerOn = false; _markerA = 0f;
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
            // Night 3, L2 (message-system section 1): deliveries at the same building within half a second are one
            // number; its text changes in place and it rises on (no jump back in opacity or place).
            if (_floatAge[j] <= 0 || _floatAge[j] > FloatMerge || _floatCell[j].DistanceSquaredTo(cell) > 4f) continue;
            _floatCoin[j] += coin;
            _floaters[j].Text = "+" + Money(Math.Max(1, _floatCoin[j]));
            return;
        }
        int live = 0;
        for (int j = 0; j < MaxFloaters; j++) if (_floatAge[j] > 0) live++;
        if (live >= FloatCap) return; // eight at most
        int i = _floatNext;
        _floatNext = (_floatNext + 1) % MaxFloaters;
        _floatCell[i] = cell;
        _floatCoin[i] = coin;
        // Round 6: anchored above the building (higher and a little north), so it clears the door and the people there.
        _floatAt[i] = new Vector3((float)x * TownGround.Cell, 9f, ((float)y - 1.5f) * TownGround.Cell);
        _floatAge[i] = 0.0001f;
        _floaters[i].Text = "+" + Money(Math.Max(1, coin));
        _floatDx[i] = FloatClearOf(i);
        _floaters[i].Visible = true;
        FloatersStarted++;
    }

    /// <summary>The sideways offset that keeps a new number off every live one (0 when it is clear where it is).</summary>
    private float FloatClearOf(int i)
    {
        var rig = _main.Rig;
        var l = _floaters[i];
        float w = l.GetMinimumSize().X, h = Math.Max(l.GetMinimumSize().Y, 1f), gap = UiKit.U(_s, 8);
        float x = rig.LocalToScreen(_floatAt[i]).X, y = rig.LocalToScreen(_floatAt[i]).Y - UiKit.U(_s, 44);
        float dx = 0f;
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            for (int j = 0; j < MaxFloaters; j++)
            {
                if (j == i || _floatAge[j] <= 0) continue;
                var o = _floaters[j];
                float ow = o.Size.X, ox = o.Position.X + ow / 2f, oy = o.Position.Y;
                // Both rise at one speed, so the height between them stays as it is now.
                if (Math.Abs(oy - y) >= h + gap) continue;
                float need = (w + ow) / 2f + gap, d = x + dx - ox;
                if (Math.Abs(d) >= need) continue;
                dx += (d >= 0 ? need - d : -need - d);
                moved = true;
            }
            if (!moved) break;
        }
        return dx;
    }

    private void StepFloaters(float dt)
    {
        // Night 3, L2: Pave and Extend fade the numbers out (0.15 s) instead of hiding them in one frame.
        _floatModeF.Want(!(PaveMode || ExtendMode));
        _floatModeF.Step(dt);
        float mode = _floatModeF.Value;
        for (int i = 0; i < MaxFloaters; i++)
        {
            if (_floatAge[i] <= 0) continue;
            _floatAge[i] += dt;
            var l = _floaters[i];
            if (_floatAge[i] > FloatSeconds) { _floatAge[i] = 0; l.Visible = false; continue; }
            // Round 6: hidden while Pave or Extend points at something (they lay over the lit cottage and the tip).
            bool show = mode > 0f;
            if (l.Visible != show) l.Visible = show;
            if (!show) continue;
            var at = _main.Rig.LocalToScreen(_floatAt[i]);
            float k = _floatAge[i] / FloatSeconds;
            l.Position = at - new Vector2(l.Size.X / 2f - _floatDx[i], UiKit.U(_s, 44) + k * UiKit.U(_s, 24));
            // A number never rises under a large message showing: it fades as the message comes (L2 review 2).
            var r = new Rect2(l.Position, l.Size);
            float under = 0f;
            if (_banner.Visible && r.Intersects(_banner.GetGlobalRect())) under = _banner.Modulate.A;
            if (_choice.Visible && r.Intersects(_choice.GetGlobalRect())) under = Math.Max(under, _choice.Modulate.A);
            l.Modulate = new Color(1, 1, 1, (1f - under) * mode * BookMotion.Smoother(Math.Clamp(Math.Min(_floatAge[i] / 0.1f, (FloatSeconds - _floatAge[i]) / 0.5f), 0f, 1f)));
        }
    }
}
