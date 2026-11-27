using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 (villagers as people, part 2): pointing at a villager. The pointer over a figure on the open map shows a ring under
/// its feet at once (TownVillagers.Pick.cs) and its name in a map tag after the tooltip's delay; a click (a left release
/// that did not drag the map) selects it and docks its card in the left column under the ledger (the Person zone): the
/// name, where it lives, its leaning in plain words, what it is doing and carrying, trips made and coin earned. A click
/// elsewhere on the map or on the card's cross closes it. Off in the Pave and Extend modes (they own the map), over the
/// interface and while paused; a click on the interface never picks. Text is made only when what it says changes.
/// </summary>
public partial class TownHud
{
    /// <summary>The villager under the pointer (-1 none) and the one whose card is open (-1 none).</summary>
    public int HoverVillager { get; private set; } = -1;
    public int SelectedVillager { get; private set; } = -1;
    /// <summary>The name tag's text while it shows ("" otherwise) and the card's lines (tests).</summary>
    public string PersonTagText => _personTag.Visible && _tagF.Value > 0.5f ? _personTag.Text : "";
    public bool PersonCardVisible => _person.Visible && _personF.On;
    public Rect2 PersonCardRect => _person.Visible ? _person.GetGlobalRect() : new Rect2();
    public string PersonCardText => _person.Visible ? $"{_pName.Text}{_pTrade.Text}|{_pHome.Text}|{_pNow.Text}|{_pCount.Text}" : "";
    /// <summary>The bottom of the card's last line on screen (tests: the card hugs its lines): n7 trades, the Trade caps.</summary>
    public float PersonLastLineBottom => _pCapsLabel.GetGlobalRect().End.Y;
    /// <summary>The close cross on the card (tests click it).</summary>
    public Button PersonClose => _pClose;
    /// <summary>The hit radius on screen now, canvas pixels.</summary>
    public float PickRadius => TownVillagers.PickRadiusPx * Math.Max(_s, 0.78f);

    private Panel _person = null!;
    private Label _personTag = null!, _pName = null!, _pHome = null!, _pLean = null!, _pNow = null!, _pCount = null!;
    private Button _pClose = null!;
    private readonly MessageFader _personF = new(TradesFadeSeconds), _tagF = new(0.12f);
    private float _hoverHeld;
    private int _hoverBefore = -1;
    private Vector2 _lastPointer = new(-1, -1);
    private float _lostFor;
    /// <summary>Seconds after a change of hover during which it does not go back to the villager it left.</summary>
    public const float FlickerHold = 0.4f;
    private Trade _tagTrade;
    private int _tagFor = -1, _cardFor = -1, _cardSig = int.MinValue, _cardCountSig = int.MinValue;

    private void BuildPerson()
    {
        var z = Zones;
        float s = _s;
        int pad = UiKit.U(s, 12), w = (int)z.Person.Size.X, inner = w - 2 * pad;
        _person = new Panel { Name = "Person", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _person.AddThemeStyleboxOverride("panel", UiKit.CardBox(s));
        _person.Position = z.Person.Position;
        _person.Size = z.Person.Size;
        _root.AddChild(_person);
        Label Line(string name, float size, Font font, Color colour, float y, float h, float right = 0)
        {
            var l = UiKit.Fixed(s, size, font, colour);
            l.Name = name; l.Position = new Vector2(pad, UiKit.U(s, y)); l.Size = new Vector2(inner - right, UiKit.U(s, h));
            _person.AddChild(l);
            return l;
        }
        int cross = UiKit.U(s, 28);
        _pName = Line("Name", UiKit.Body, UiKit.UiBold, UiKit.Ink, 8, 24, cross);
        _pName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _pHome = Line("Home", UiKit.Caption, UiKit.UiFont, UiKit.Muted, 33, 20);
        _pLean = Line("Leaning", UiKit.Caption, UiKit.UiFont, UiKit.Ink, 56, 20);
        _pNow = Line("Now", UiKit.Caption, UiKit.UiFont, UiKit.Ink, 56, 20);
        _pCount = Line("Counts", UiKit.Caption, UiKit.TabRegular, UiKit.Muted, 79, 20);
        _pLean.Visible = false; // n7 trades: the like is said on line 2 ("Likes reaping · by the well lane")
        _pName.Size = new Vector2(inner - cross, _pName.Size.Y);
        BuildPersonTrade(pad, inner - cross);
        // Sized to its text by the coin sign's layout (SetCoinText resets the size): no clipping, or it would shrink to nothing.
        _pCount.ClipText = false;
        _pCount.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        _pClose = new Button { Name = "Close", Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "" };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" }) _pClose.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
        _pClose.AddThemeFontSizeOverride("font_size", UiKit.Type(s, UiKit.Title));
        _pClose.AddThemeColorOverride("font_color", UiKit.Muted);
        _pClose.AddThemeColorOverride("font_hover_color", UiKit.Ink);
        _person.AddChild(_pClose); // in the tree first, so its measure uses the interface's theme
        // Inside the card's top right corner, as large as its glyph (never wider than the card leaves it).
        var cs = _pClose.GetMinimumSize();
        cs = new Vector2(Math.Max(cs.X, cross * 0.8f), Math.Min(Math.Max(cs.Y, cross * 0.8f), UiKit.U(s, 30)));
        _pClose.Size = cs;
        _pClose.Position = new Vector2(w - UiKit.U(s, 6) - cs.X, UiKit.U(s, 4));
        _pClose.Pressed += () => Select(-1);
        _personTag = Tag("PersonTag", UiKit.Caption);
        BuildTrades();
        BuildAutoTags();
        _personF.Set(SelectedVillager >= 0);
        _tagF.Set(false);
        _cardFor = -1; _cardSig = int.MinValue; _cardCountSig = int.MinValue; _tagFor = -1;
        // Warm the pick and the names once here (first calls compile and set up), not in the frame the pointer first
        // comes to rest on someone.
        _main.Villagers.Pick(_main.Rig, Vector2.Zero, 1f);
        for (int i = 0; i < _drv.Town.VillagerCount; i++) _drv.Town.VillagerName(i);
        PersonTarget(new Vector2(-1, -1));
    }

    /// <summary>Opens the card of villager <paramref name="id"/> (-1 closes it).</summary>
    public void Select(int id)
    {
        if (id >= _drv.Town.VillagerCount) id = -1;
        SelectedVillager = id;
        _main.Villagers.SelectedId = id;
    }

    /// <summary>Whether pointing at villagers is on now: the open map, no click mode, not paused, no page over the map.</summary>
    private bool PickingOn => !(_main.Menu != null && _main.Menu.IsPaused) && !PaveMode && !ExtendMode && _fullA > 0.5f
        && !AchievementsOpen && !AgePageOpen && _fade > 0.5f;

    /// <summary>The villager a player pointing here would pick now (-1: none, or picking is off here).</summary>
    public int PersonTarget(Vector2 screen)
    {
        if (!PickingOn || OverInterface(screen) || OverPerson(screen) || OverTrades(screen)) return -1;
        if (!new Rect2(Vector2.Zero, _built).HasPoint(screen)) return -1;
        return _main.Villagers.Pick(_main.Rig, screen, PickRadius, HoverVillager);
    }

    private bool OverPerson(Vector2 p) => Over(_person, p);

    private void PeopleInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && !_main.Rig.LeftDragged)
        {
            if (!PickingOn || OverInterface(mb.Position) || OverPerson(mb.Position) || OverTrades(mb.Position)) return;
            int id = _main.Villagers.Pick(_main.Rig, mb.Position, PickRadius, HoverVillager);
            Select(id); // a click on the open map away from everyone closes the card
            // Not marked handled: the camera must see the release too.
        }
    }

    private void PeopleFrame(float dt)
    {
        var t = _drv.Town;
        TradesFrame(dt);
        AutoTagsFrame(dt);
        // The pointer as the system has it (an _Input override to follow events cost a managed object per event:
        // game-motion's allocation gate). Picture runs under xvfb move the system pointer with Input.WarpMouse.
        var p = _root.GetGlobalMousePosition();
        bool gui = GetViewport().GuiGetHoveredControl() != null;
        int h = gui ? -1 : PersonTarget(p);
        // No flicker: the hover does not go back to the one it has just left within FlickerHold seconds while it is on
        // someone (two neighbours walking under a still pointer change places at the edge of the radius).
        if (h >= 0 && h == _hoverBefore && HoverVillager >= 0 && _hoverHeld < FlickerHold) h = HoverVillager;
        // Under a still pointer a figure that steps just out of reach keeps the hover a moment (it may step back).
        if (h < 0 && HoverVillager >= 0 && !gui && p == _lastPointer && _lostFor < FlickerHold && PickingOn && !OverInterface(p)) { h = HoverVillager; _lostFor += dt; }
        else if (h >= 0) _lostFor = 0f;
        _lastPointer = p;
        if (h != HoverVillager) { _hoverBefore = HoverVillager; HoverVillager = h; _hoverHeld = 0f; }
        else if (h >= 0) _hoverHeld += dt;
        _main.Villagers.HoverId = h;
        if (SelectedVillager >= t.VillagerCount) Select(-1);
        if (SelectedVillager >= 0 && _main.Villagers.SelectedId != SelectedVillager) _main.Villagers.SelectedId = SelectedVillager;
        // The name tag: after the tooltip's delay, above the figure's head, in the map's tag style.
        _tagF.Want(h >= 0 && _hoverHeld >= TipDelay);
        _tagF.Step(dt);
        // n7 trades: "Joss, reaper" (made again when the trade changes).
        if (h >= 0 && (_tagFor != h || t.Villager(h).Trade != _tagTrade))
        {
            _tagFor = h; _tagTrade = t.Villager(h).Trade;
            _personTag.Text = t.VillagerName(h) + ", " + TradeOne[(int)_tagTrade];
            _personTag.ResetSize();
        }
        bool tag = _tagF.Value > 0.001f && _tagFor >= 0 && _tagFor < t.VillagerCount;
        if (_personTag.Visible != tag) _personTag.Visible = tag;
        if (tag)
        {
            var v = _main.Villagers;
            var head = _main.Rig.LocalToScreen(v.FeetLocal(_tagFor) + new Vector3(0, v.FigureHeight, 0));
            _personTag.Position = (head - new Vector2(_personTag.Size.X / 2f, _personTag.Size.Y + UiKit.Px(_s, 6))).Round();
            _personTag.Modulate = new Color(1, 1, 1, _tagF.Value);
        }
        // The card: fades in and out in its zone (0.15 s), and with the interface when zoomed out.
        _personF.Want(SelectedVillager >= 0);
        _personF.Step(dt);
        float a = _personF.Value * _fullA;
        bool show = a > 0.001f;
        if (_person.Visible != show) _person.Visible = show;
        if (!show) return;
        _person.Modulate = new Color(1, 1, 1, a);
        if (SelectedVillager >= 0) CardText(t, SelectedVillager);
    }

    /// <summary>The card's lines, made only when what they say changes.</summary>
    private void CardText(Town t, int id)
    {
        var v = t.Villager(id);
        if (id != _cardFor)
        {
            _cardFor = id; _cardSig = int.MinValue; _cardCountSig = int.MinValue;
            // n7 trades: one size for both cards (HudZones.PersonH, 156): the title with its trade, the like and the
            // home, now, the counts, the Trade caps (TownHud.PeopleTrade.cs).
        }
        PersonTradeFrame(t, id, v);
        int sig = HashCode.Combine(v.Phase, v.Job, v.Carry, v.Stage, v.QueuePosition, v.Workplace, v.OnRiver);
        if (sig != _cardSig) { _cardSig = sig; _pNow.Text = NowWords(t, v); }
        int csig = HashCode.Combine(v.Trips, (long)Math.Floor(v.Earned), _s);
        if (csig != _cardCountSig)
        {
            _cardCountSig = csig;
            SetCoinText(_pCount, (v.Trips == 1 ? "1 trip" : $"{v.Trips} trips") + " · earned ", v.Earned, "");
        }
    }

    /// <summary>"A good hand with water": a leaning in the interface's plain words.</summary>
    public static string LeaningWords(Leaning l) => l switch
    {
        Leaning.Water => "A good hand with water",
        Leaning.Grain => "A good hand at the harvest",
        Leaning.Flour => "A good hand at the mill",
        Leaning.Wool => "A good hand with sheep",
        Leaning.Cloth => "A good hand at the loom",
        _ => "Turns a hand to anything",
    };

    /// <summary>"Lives by the well lane": the place nearest the villager's cottage, or the river for a river cottage.</summary>
    public static string HomeWords(Town t, VillagerView v)
    {
        if (v.RiverHome >= 0) return "Lives by the river";
        var plots = t.Plan.Plots;
        if (v.Home < 0 || v.Home >= plots.Length) return "Lives in the town";
        var home = plots[v.Home];
        float hx = home.X + home.W / 2f, hy = home.Y + home.H / 2f, best = float.MaxValue;
        PlotKind kind = PlotKind.Cottage;
        foreach (var p in plots)
        {
            if (p.Kind == PlotKind.Cottage || !t.PlotGiven(p.Id)) continue;
            float dx = p.X + p.W / 2f - hx, dy = p.Y + p.H / 2f - hy, d = dx * dx + dy * dy;
            if (d < best) { best = d; kind = p.Kind; }
        }
        return kind switch
        {
            PlotKind.Well => "Lives by the well lane",
            PlotKind.Field => "Lives by the fields",
            PlotKind.Market => "Lives near the market",
            PlotKind.Mill => "Lives by the mill",
            PlotKind.BellGreen => "Lives by the green",
            _ => "Lives in the town",
        };
    }

    private static string GoodWord(Good g) => g switch
    {
        Good.Water => "water", Good.Grain => "grain", Good.Flour => "flour", Good.Wool => "wool", Good.Cloth => "cloth", _ => "goods",
    };

    private static string PlaceOf(Town t, int workplace) => workplace >= 0 && workplace < t.WorkplaceCount ? t.Workplace(workplace).Kind switch
    {
        PlotKind.Well => "the well", PlotKind.Field => "the field", PlotKind.Mill => "the mill", PlotKind.Market => "the market", _ => "work",
    } : "work";

    private static readonly string[] Ordinals = { "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth" };

    /// <summary>What the villager is doing now, in a few plain words.</summary>
    public static string NowWords(Town t, VillagerView v)
    {
        bool river = v.Job is JobKind.GrainBoat or JobKind.FlourBoat or JobKind.WoolBoat or JobKind.ClothBoat;
        switch (v.Phase)
        {
            case VillagerPhase.Working:
                if (v.OnRiver) return v.Job is JobKind.ClothBoat or JobKind.ClothMarket && v.Carry == Good.Wool ? "Weaving cloth" : "Shearing the sheep";
                return PlaceOf(t, v.Workplace) switch { "the well" => "Drawing water at the well", "the field" => "Reaping in the field", "the mill" => "Grinding at the mill", _ => "At work" };
            case VillagerPhase.Queued:
                if (v.QueuePosition >= 0)
                    return (v.QueuePosition < Ordinals.Length ? Ordinals[v.QueuePosition] : $"No. {v.QueuePosition + 1}") + " in line at " + PlaceOf(t, v.Workplace);
                return v.OnRiver ? "Waiting to cross the river" : "Waiting";
            case VillagerPhase.Walking:
                if (v.Carry != Good.None)
                {
                    string to = river || (v.OnRiver && v.Job is JobKind.WoolBoat or JobKind.ClothBoat) ? "the landing"
                        : v.Job is JobKind.Water or JobKind.GrainHome ? "home"
                        : v.Stage == 1 ? "the mill" : "the market";
                    if (v.Job is JobKind.ClothBoat or JobKind.ClothMarket && v.Carry == Good.Wool) to = "the weaver";
                    return $"Carrying {GoodWord(v.Carry)} to {to}";
                }
                if (v.Job == JobKind.None) return "Looking for work";
                if (v.Job is JobKind.WoolBoat or JobKind.WoolMarket or JobKind.ClothBoat or JobKind.ClothMarket) return "On the way to the pasture";
                return v.Stage >= 2 ? "On the way home" : "On the way to " + PlaceOf(t, v.Workplace);
            default:
                return "At home";
        }
    }
}
