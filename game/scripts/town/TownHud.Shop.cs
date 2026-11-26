using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The shop's data, keys and rows' content (round 3, rules v2; night 3 layout in TownHud.ShopRows.cs and ShopRow.cs):
/// every shop item the simulation lists, grouped People / Work / Roads / Town / The river, each with its own key. The
/// Pave and Extend rows switch their click modes (TownHud.Pave.cs, TownHud.Extend.cs) instead of buying.
/// </summary>
public partial class TownHud
{
    public enum ShopGroup { People, Work, Roads, Town, River }
    private static readonly string[] GroupNames = { "People", "Work", "Roads", "Town", "The river" };

    private readonly List<Button> _marks = new();
    private readonly List<ShopItem> _items = new();
    private readonly List<ShopItem> _shown = new();

    /// <summary>The bookmark button for a shop item kind (first one; tests), or null.</summary>
    public Button? Mark(ShopItemKind kind)
    {
        for (int i = 0; i < _shown.Count && i < _marks.Count; i++) if (_shown[i].Kind == kind) return _marks[i];
        return null;
    }
    /// <summary>The bookmark for a shop item kind and index (Value items: the good; offers: the offer id).</summary>
    public Button? Mark(ShopItemKind kind, int index)
    {
        for (int i = 0; i < _shown.Count && i < _marks.Count; i++) if (_shown[i].Kind == kind && (kind is ShopItemKind.Pave or ShopItemKind.Extend || _shown[i].Index == index)) return _marks[i];
        return null;
    }
    /// <summary>The fill of a row's "not yet" line toward its price (0..1; tests).</summary>
    public float MarkBar(int i) => i >= 0 && i < _rowOrder.Count && _rowOrder[i].State == ShopRow.PillState.NotYet ? _rowOrder[i].FillK : 0f;
    /// <summary>The rows shown, in order (locked and finished purchases are not rows), and their buttons.</summary>
    public IReadOnlyList<ShopItem> ShownItems => _shown;
    public IReadOnlyList<Button> Marks => _marks;

    public static ShopGroup GroupOf(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager or ShopItemKind.Extend => ShopGroup.People,
        ShopItemKind.Pace or ShopItemKind.Load or ShopItemKind.Tools or ShopItemKind.Value => ShopGroup.Work,
        ShopItemKind.Pave => ShopGroup.Roads,
        >= ShopItemKind.Ferry and <= ShopItemKind.Winch => ShopGroup.River,
        _ => ShopGroup.Town,
    };

    /// <summary>The InputMap action of a shown item; building offers take F, G, H in the order they are shown.</summary>
    public static string ActionFor(in ShopItem it, int offerOrdinal) => it.Kind switch
    {
        ShopItemKind.Villager => "tt_buy_1",
        ShopItemKind.Pace => "tt_buy_2",
        ShopItemKind.Load => "tt_buy_3",
        ShopItemKind.Pave => "tt_buy_4",
        ShopItemKind.Extend => "tt_buy_5",
        ShopItemKind.Tools => "tt_buy_6",
        ShopItemKind.Value => it.Index == (int)Good.Water ? "tt_buy_7" : it.Index == (int)Good.Grain ? "tt_buy_8" : "tt_buy_9",
        ShopItemKind.BellTrack => "tt_buy_0",
        ShopItemKind.Building => offerOrdinal switch { 0 => "tt_offer_1", 1 => "tt_offer_2", 2 => "tt_offer_3", _ => "" },
        _ => "",
    };

    /// <summary>The key printed on a bookmark: the action's first key (rebinding shows), plus P for Pave.</summary>
    public static string KeyLabel(string action, ShopItemKind kind)
    {
        // Round 5: cached per binding version. Reading the InputMap makes Godot objects (the action's events) that the
        // engine glue tracks; at 8x every bookmark's text changes nearly every frame and that tracking grew by 1.3 MB.
        if (_keyLabelVersion != Settings.GameInput.Version) { _keyLabels.Clear(); _keyLabelVersion = Settings.GameInput.Version; }
        if (_keyLabels.TryGetValue((action, kind), out var cached)) return cached;
        string made = MakeKeyLabel(action, kind);
        _keyLabels[(action, kind)] = made;
        return made;
    }

    private static readonly Dictionary<(string, ShopItemKind), string> _keyLabels = new();
    private static int _keyLabelVersion = int.MinValue;

    /// <summary>The one key printed in a row's key cap: the action's first bound key ("" none).</summary>
    public static string FirstKey(string action) => KeyLabel(action, ShopItemKind.Villager);

    private static string MakeKeyLabel(string action, ShopItemKind kind)
    {
        if (action == "" || !InputMap.HasAction(action)) return "";
        string k = "";
        foreach (var ev in InputMap.ActionGetEvents(action))
            if (ev is InputEventKey key) { k = Settings.GameInput.KeyName(Settings.GameInput.KeyOf(key)); break; }
        if (kind == ShopItemKind.Pave && InputMap.HasAction("tt_pave"))
            foreach (var ev in InputMap.ActionGetEvents("tt_pave"))
                if (ev is InputEventKey key) { k += " or " + Settings.GameInput.KeyName(Settings.GameInput.KeyOf(key)); break; }
        return k;
    }


    private static string IconFor(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "up-villager", ShopItemKind.Pace => "up-pace", ShopItemKind.Load => "up-load",
        ShopItemKind.Pave => "up-pave", ShopItemKind.Extend => "up-extend", ShopItemKind.Tools => "up-tools",
        ShopItemKind.Value => "up-value", ShopItemKind.BellTrack => "ui-bell",
        ShopItemKind.Ferry or ShopItemKind.Raft or ShopItemKind.Rope => "ui-ferry", ShopItemKind.Berth => "ui-boat-berth",
        ShopItemKind.Crane => "ui-crane", ShopItemKind.BoatPrices => "ui-boat", ShopItemKind.Toll => "ui-toll", ShopItemKind.Winch => "ui-bridge",
        ShopItemKind.Regulars => "ui-value", ShopItemKind.Hand => "up-villager",
        _ => "up-building",
    };

    /// <summary>Sheet section 4: the shop's own icons by name (the asset track draws them; stand-ins until then).</summary>
    public static string ShopIconName(in ShopItem it) => "shop-" + it.Kind switch
    {
        ShopItemKind.Value or ShopItemKind.RiverValue => "value-" + GoodName((Good)it.Index).ToLowerInvariant(),
        ShopItemKind.BellTrack => "bell",
        _ => it.Kind.ToString().ToLowerInvariant(),
    };

    private static string StandInIcon(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "ui-reputation", ShopItemKind.Pace => "ui-speed", ShopItemKind.Load => "ui-boat",
        ShopItemKind.Pave => "tool-gravel", ShopItemKind.Extend => "up-building", ShopItemKind.Tools => "up-load",
        ShopItemKind.Value => "ui-coin", ShopItemKind.BellTrack => "ui-sound-on", ShopItemKind.Regulars => "ui-coin",
        ShopItemKind.Hand => "ui-reputation", _ => "ui-new-map",
    };

    /// <summary>A step that cannot be bought yet: locked by level or by a building still to come.</summary>
    public static bool IsLocked(in ShopItem it) => it.Locked;
    /// <summary>A track bought to its end (round 5: the simulation's flags, no longer read from the reason's words).</summary>
    public static bool IsFinished(in ShopItem it) => it.Finished;

    private void BuildShop()
    {
        BuildShopCard();
        BuildTimers();
    }


    /// <summary>Buys the shown item at a row (the Pave and Extend rows toggle their modes instead).</summary>
    public void Activate(int i)
    {
        if (i < 0 || i >= _shown.Count) return;
        ActivateItem(_shown[i]);
    }

    private void ActivateItem(ShopItem it)
    {
        if (it.Kind == ShopItemKind.Pave) { _main.Audio.UiPress(); SetPaveMode(!PaveMode); return; }
        if (it.Kind == ShopItemKind.Extend) { _main.Audio.UiPress(); SetExtendMode(!ExtendMode); return; }
        // Read the item afresh: prices change after every purchase.
        _drv.Town.ShopItems(_items);
        foreach (var cur in _items)
            if (cur.Kind == it.Kind && cur.Index == it.Index)
            {
                if (!_drv.Town.Buy(cur)) { _main.Audio.Cannot(); Note(cur.Reason.Length > 0 ? cur.Reason + "." : "Not yet."); }
                return;
            }
    }

    private static bool SameSet(List<ShopItem> a, List<ShopItem> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Kind != b[i].Kind || (a[i].Index != b[i].Index && a[i].Kind is not (ShopItemKind.Pave or ShopItemKind.Extend))) return false;
        return true;
    }

    /// <summary>The shown items in display order: grouped, the simulation's order kept inside a group.</summary>
    private void Grouped(List<ShopItem> src, List<ShopItem> dst)
    {
        dst.Clear();
        for (int g = 0; g < GroupNames.Length; g++)
            foreach (var it in src) if ((int)GroupOf(it.Kind) == g && !RiverHidden(it)) dst.Add(it);
    }

    private readonly List<ShopItem> _raw = new();

    /// <summary>Round 4: the river's group shows only what can be bought now or soon (coin the one thing missing):
    /// locked and finished river steps are left out, so the bookmarks fit at the end of the second age.</summary>
    public static bool RiverHidden(in ShopItem it) => GroupOf(it.Kind) == ShopGroup.River && (it.Locked || it.Finished);

    private void ShopFrame(float dt)
    {
        var t = _drv.Town;
        t.ShopItems(_raw);
        Grouped(_raw, _items);
        SyncRows(t, PointerOverShop());
        int offer = 0;
        for (int i = 0; i < _rowOrder.Count; i++)
        {
            var r = _rowOrder[i];
            var it = r.Item;
            // Pave's pill shows the cheapest road that can be paved now ("from"; no tip shows less).
            if (it.Kind == ShopItemKind.Pave) it.Price = CheapestPave(it.Price);
            int ord = it.Kind == ShopItemKind.Building ? offer++ : 0;
            bool poor = it.Reason == "Not enough coin";
            bool mode = it.Kind == ShopItemKind.Pave ? PaveMode : it.Kind == ShopItemKind.Extend && ExtendMode;
            var st = mode ? ShopRow.PillState.On : it.Affordable ? ShopRow.PillState.Affordable : poor ? ShopRow.PillState.NotYet : ShopRow.PillState.Blocked;
            long need = poor ? (long)Math.Floor(Math.Max(1, it.Price - t.Coin)) : 0;
            int worth = WorthTenths(it.Worth);
            int sig = HashCode.Combine((int)it.Kind, it.Level, (long)Math.Floor(it.Price), (int)st, it.Reason, need, ord, worth);
            sig = HashCode.Combine(sig, it.Name, Settings.GameInput.Version, r.Tall);
            r.Button.Disabled = !(it.Affordable || mode);
            if (sig != r.Sig)
            {
                r.Sig = sig;
                RowContent(r, it, ord, st, need, worth);
            }
            if (st == ShopRow.PillState.NotYet) r.SetFill(it.Price > 0 ? (float)Math.Clamp(t.Coin / it.Price, 0, 1) : 0f);
        }
        LayoutShop(dt, t);
        FoldFrame(t);
        AffordFrame();
        PulseFrame(dt);
    }

    private void RowContent(ShopRow r, in ShopItem it, int ord, ShopRow.PillState st, long need, int worth)
    {
        if (r.Icon.Texture == null)
            r.Icon.Texture = IconOr(ShopIconName(it), it.Kind == ShopItemKind.RiverValue ? (it.Index == (int)Good.Wool ? "good-wool" : "good-cloth") : IconFor(it.Kind))
                             ?? IconOr(StandInIcon(it.Kind), "ui-coin");
        // Each part is set only when it changed (the signature changes with every coin while saving up).
        string key = FirstKey(ActionFor(it, ord));
        if (key != r.KeyShown) { r.KeyShown = key; r.Key.Text = key; r.Key.Visible = key.Length > 0; }
        bool track = it.Kind is ShopItemKind.Villager or ShopItemKind.Pace or ShopItemKind.Load or ShopItemKind.Tools or ShopItemKind.Value or ShopItemKind.BellTrack or ShopItemKind.Regulars;
        string name = RowName(it);
        int step = track && it.Level > 0 ? it.Level : 0;
        if (name != r.NameShown || step != r.StepShown)
        {
            r.NameShown = name; r.StepShown = step;
            r.Name.Text = name;
            r.Step.Text = step > 0 ? step.ToString(Inv) : "";
            r.PlaceStep();
        }
        string w = worth > 0 ? WorthLine(worth) : "";
        string state = st switch
        {
            ShopRow.PillState.On => it.Kind == ShopItemKind.Pave ? "Click a lit road" : "Click a lit cottage",
            ShopRow.PillState.NotYet => $"{Money(need)} short",
            ShopRow.PillState.Blocked => it.Reason.Length > 0 ? it.Reason : "Not yet",
            _ => "",
        };
        string line = w.Length > 0 && state.Length > 0 ? w + " · " + state : w + state;
        // An optional caption is dropped whole, never cut: the worth goes first (it stays in the tooltip).
        if (state.Length > 0 && TextWidth(r.Line2, line) > r.Line2.Size.X) line = state;
        if (line != r.Line2Shown) { r.Line2Shown = line; r.Line2.Text = line; }
        if (st != r.State || !r.StateSet) { r.StateSet = true; r.SetState(st); r.PriceShown = ""; }
        string price = st == ShopRow.PillState.On ? "On" : it.Price > 0 && !double.IsInfinity(it.Price) ? (it.Kind == ShopItemKind.Pave ? "from " : "") + UiKit.Short(it.Price)
            : it.Kind == ShopItemKind.Pave ? "no road" : "–"; // L1 fix: a blocked Pave says why, not an empty box
        if (price != r.PriceShown)
        {
            r.PriceShown = price;
            r.Price.Text = price;
            r.FromPrice(it.Kind == ShopItemKind.Pave);
            r.FitPrice();
        }
    }

    private double CheapestPave(double fallback)
    {
        var t = _drv.Town;
        double best = double.MaxValue;
        for (int r = 0; r < t.RoadCount; r++) if (Pavable(r)) best = Math.Min(best, t.PavePrice(r));
        return best < double.MaxValue ? best : fallback;
    }

    private static string RowName(in ShopItem it) => it.Kind switch
    {
        ShopItemKind.Pave => "Pave a road",
        ShopItemKind.Extend => "Extend a cottage",
        _ => it.Name,
    };

    /// <summary>"+3.7 a minute": what one more is worth (line 2 of a row).</summary>
    public static string WorthLine(int tenths) => tenths < 100 && tenths % 10 != 0 ? $"+{tenths / 10}.{tenths % 10} a minute" : $"+{Money(tenths / 10)} a minute";

    /// <summary>Round 6: the bell's whole sentence while it rings (its tooltip).</summary>
    public static string BellRingingText(int left, int restAfter) => restAfter > 0 ? $"Ringing: {left} s left, then rests {restAfter} s" : $"Ringing: {left} s left, ready again after";

    /// <summary>Call when key bindings changed, so the bookmarks print the new keys.</summary>
    /// <summary>Kept for callers: the keys are reprinted by themselves when the bindings change (GameInput.Version).</summary>
    public void KeysChanged() { }

    /// <summary>Round 5: the simulation's worth (coin a minute) as shown: tenths below 10, whole numbers above.</summary>
    public static int WorthTenths(double w) => w <= 0.05 ? 0 : w < 10 ? (int)Math.Round(w * 10) : (int)Math.Round(w) * 10;
    public static string WorthText(int tenths) => tenths < 100 && tenths % 10 != 0 ? $"+{tenths / 10}.{tenths % 10} coin a minute" : $"+{Money(tenths / 10)} coin a minute";

    /// <summary>Round 6: "Unlocks at level 25" reads "level 25" on a folded line.</summary>
    private static string ShortLock(string reason) => reason.StartsWith("Unlocks at ") ? reason.Substring("Unlocks at ".Length) : reason;

    /// <summary>"Unlocks at level 7" reads "at level 7" on a group's heading line.</summary>
    private static string FoldLock(string reason) => reason.StartsWith("Unlocks ") ? reason.Substring("Unlocks ".Length) : reason;

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    private static string Tooltip(in ShopItem it) => it.Kind switch
    {
        ShopItemKind.Villager => "One more villager. Each cottage houses two, more when extended.",
        ShopItemKind.Pace => "Everyone walks a little faster.",
        ShopItemKind.Load => "Everyone carries one more unit per trip.",
        ShopItemKind.Tools => "Work at the well, field and mill goes faster.",
        ShopItemKind.Value => "This good pays more for every unit delivered.",
        ShopItemKind.BellTrack => "The bell hurries everyone a little longer and rests a little less.",
        ShopItemKind.Extend => "One more room in a cottage: room for one more villager. Click a lit cottage.",
        ShopItemKind.Pave => "Pave one worn-in road: faster, and it carries more people. Click a lit road.",
        ShopItemKind.Ferry => "A ferry over the river: it crosses at any tide, a few people at a time.",
        ShopItemKind.Berth => "A second berth at the landing: two boats moor at once.",
        ShopItemKind.Crane => "A crane at the landing: boats ask for bigger cargoes.",
        ShopItemKind.BoatPrices => "Boats pay more for every unit.",
        ShopItemKind.RiverValue => "This good pays more for every unit delivered.",
        ShopItemKind.Raft => "A bigger raft: the ferry carries one more person.",
        ShopItemKind.Rope => "A faster rope: the ferry crosses quicker.",
        ShopItemKind.Toll => "Every boat through the drawbridge pays more toll.",
        ShopItemKind.Winch => "A faster winch: the drawbridge lifts quicker, so walkers wait less.",
        ShopItemKind.Regulars => RegularsHelp,
        ShopItemKind.Hand => "One more place to work where people queue. Its workplace shows a marker.",
        _ => "A building for the town, placed on its plot by the town.",
    };

    public const string RegularsHelp = "A regular customer: every delivery pays a little more.";

    public override void _UnhandledInput(InputEvent e)
    {
        // Round 4: while the book is closing, closed, on the settings pages or opening, the town's interface takes no
        // keys and no clicks at all (a hidden CanvasLayer still gets unhandled input).
        if (_main.Menu == null || _main.Menu.IsPaused) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            bool handled = true;
            if (Is(e, "tt_pave")) SetPaveMode(!PaveMode);
            else if (Is(e, "tt_bell")) RingBell();
            else if (Is(e, "tt_next_age") && (_drv.Town.CanRingNextAge || _drv.Town.NextAgeRung)) NextAgePressed();
            else if (Is(e, "tt_achievements")) ToggleAchievements();
            else if ((Is(e, "tt_pause") || k.Keycode == Key.Escape) && (AchievementsOpen || AgePageOpen)) { CloseAchievements(); CloseAgePage(); }
            else if ((Is(e, "tt_pause") || k.Keycode == Key.Escape) && (PaveMode || ExtendMode)) { SetPaveMode(false); SetExtendMode(false); }
            else if (ChoiceKey(e)) { }
            else if (!ActivateByKey(e)) handled = false;
            if (handled) GetViewport().SetInputAsHandled();
            return;
        }
        PaveInput(e);
        ExtendInput(e);
    }

    /// <summary>The purchase whose own key this is: a row (offers take F, G, H in the order the rows show them), or a
    /// locked step that is not a row (its key still answers with why, as before).</summary>
    private bool ActivateByKey(InputEvent e)
    {
        int offer = 0;
        for (int i = 0; i < _shown.Count; i++)
        {
            int ord = _shown[i].Kind == ShopItemKind.Building ? offer++ : 0;
            string a = ActionFor(_shown[i], ord);
            if (a.Length > 0 && Is(e, a)) { Activate(i); return true; }
        }
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Kind == ShopItemKind.Building) continue;
            string a = ActionFor(_items[i], 0);
            if (a.Length > 0 && Is(e, a)) { ActivateItem(_items[i]); return true; }
        }
        return false;
    }

    /// <summary>"P to stop" with the key bound now (slot 1: the second key, E for Extend, when there is one).</summary>
    private static string StopKey(string action, int slot = 0)
    {
        if (!InputMap.HasAction(action)) return "";
        int i = 0; string first = "";
        foreach (var ev in InputMap.ActionGetEvents(action))
            if (ev is InputEventKey k)
            {
                string n = Settings.GameInput.KeyName(Settings.GameInput.KeyOf(k));
                if (i == 0) first = n;
                if (i == slot) return n + " to stop";
                i++;
            }
        return first.Length > 0 ? first + " to stop" : "";
    }

    private static bool Is(InputEvent e, string action) => InputMap.HasAction(action) && e.IsActionPressed(action);
}
