using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The shop side of the interface (round 3, rules v2): every shop item the simulation lists is a bookmark down the
/// right edge, in four groups (People, Work, Roads, Town). A bookmark shows its name, track level, its own key, the
/// price and either "Buy", how much coin is still needed (with a bar filling toward the price), a locked step with its
/// reason ("Unlocks at level 9") or a finished track. The Pave and Extend bookmarks switch their click modes
/// (TownHud.Pave.cs, TownHud.Extend.cs) instead of buying. The bell is at the bottom left with its rest time, and the
/// charter's "Ring in the next age" above it.
/// </summary>
public partial class TownHud
{
    public enum ShopGroup { People, Work, Roads, Town }
    private static readonly string[] GroupNames = { "People", "Work", "Roads", "Town" };

    private VBoxContainer _shop = null!;
    private readonly List<Button> _marks = new();
    private readonly List<ColorRect> _markBars = new();
    private readonly List<Control> _heads = new();
    private readonly List<ShopItem> _items = new();
    private readonly List<ShopItem> _shown = new();
    private Button _bell = null!, _nextAge = null!;
    private int[] _markSig = Array.Empty<int>();
    private int _bellKey = int.MinValue, _bellKeysVersion = -1, _nextAgeSig = -1;

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
    /// <summary>The fill of a bookmark's bar toward its price (0..1; tests).</summary>
    public float MarkBar(int i) => i >= 0 && i < _markBars.Count && _markBars[i].Visible ? _markBars[i].Size.X / Math.Max(1f, _marks[i].Size.X - 2f) : 0f;
    public IReadOnlyList<ShopItem> ShownItems => _shown;
    public IReadOnlyList<Button> Marks => _marks;
    public Button BellButton => _bell;
    public Button NextAgeButton => _nextAge;

    public static ShopGroup GroupOf(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager or ShopItemKind.Extend => ShopGroup.People,
        ShopItemKind.Pace or ShopItemKind.Load or ShopItemKind.Tools or ShopItemKind.Value => ShopGroup.Work,
        ShopItemKind.Pave => ShopGroup.Roads,
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
        ShopItemKind.Value => "up-value", ShopItemKind.BellTrack => "ui-bell", _ => "up-building",
    };

    private static string StandInIcon(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "ui-reputation", ShopItemKind.Pace => "ui-speed", ShopItemKind.Load => "ui-boat",
        ShopItemKind.Pave => "tool-gravel", ShopItemKind.Extend => "up-building", ShopItemKind.Tools => "up-load",
        ShopItemKind.Value => "ui-coin", ShopItemKind.BellTrack => "ui-sound-on", _ => "ui-new-map",
    };

    /// <summary>A step that cannot be bought yet: locked by level or by a building still to come.</summary>
    public static bool IsLocked(in ShopItem it) => it.Reason.StartsWith("Unlocks at level") || it.Reason.StartsWith("Comes with");
    /// <summary>A track bought to its end.</summary>
    public static bool IsFinished(in ShopItem it) => it.Reason.EndsWith("reached") || it.Reason.Contains("at its best");

    private void BuildShop()
    {
        _shop = new VBoxContainer { Name = "Shop", MouseFilter = Control.MouseFilterEnum.Ignore };
        _shop.AddThemeConstantOverride("separation", UiKit.Px(_s, 4));
        _root.AddChild(_shop);
        _marks.Clear();
        _markBars.Clear();
        _heads.Clear();
        _shown.Clear();
        _markSig = Array.Empty<int>();
        _bellKey = int.MinValue; _nextAgeSig = -1;

        _bell = MakeButton("Bell", "ui-bell", "ui-sound-on");
        _bell.Pressed += RingBell;
        _root.AddChild(_bell);
        _nextAge = MakeButton("NextAge", "ui-achievement", "ui-reputation");
        _nextAge.Pressed += NextAgePressed;
        _nextAge.Visible = false;
        _root.AddChild(_nextAge);
    }

    private Button MakeButton(string name, string icon, string standIn, float fontPx = 16, float margin = 8)
    {
        var b = new Button
        {
            Name = name, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None,
            Icon = IconOr(icon, standIn), ExpandIcon = false, ClipText = false,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        b.AddThemeConstantOverride("icon_max_width", UiKit.Px(_s, 26));
        b.AddThemeConstantOverride("h_separation", UiKit.Px(_s, 9));
        b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, fontPx));
        // Bookmarks: paper slips with an ink edge; a slip that can be bought now is brighter.
        var normal = UiKit.Card(_s, UiKit.Paper, 1.5f);
        normal.SetContentMarginAll(UiKit.Px(_s, margin));
        var hover = UiKit.Card(_s, UiKit.PaperHover, 2f);
        hover.SetContentMarginAll(UiKit.Px(_s, margin));
        var pressed = UiKit.Card(_s, UiKit.Pressed, 2.5f);
        pressed.SetContentMarginAll(UiKit.Px(_s, margin));
        var off = UiKit.Card(_s, UiKit.PaperDark, 1f);
        off.SetContentMarginAll(UiKit.Px(_s, margin));
        off.BorderColor = UiKit.FadedInk;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", off);
        b.AddThemeColorOverride("font_disabled_color", UiKit.Ink.Lerp(UiKit.Paper, 0.25f));
        b.MouseEntered += () => _main.Audio.UiHover();
        return b;
    }

    private void RingBell()
    {
        if (!_drv.Town.RingBell()) Note(_drv.Town.Level < 2 ? "The bell comes at level 2." : $"The bell is resting: {Math.Ceiling(_drv.Town.BellWait)} s.");
    }

    /// <summary>Buys the shown item at a bookmark (the Pave and Extend bookmarks toggle their modes instead).</summary>
    public void Activate(int i)
    {
        if (i < 0 || i >= _shown.Count) return;
        var it = _shown[i];
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
        for (int g = 0; g < 4; g++)
            foreach (var it in src) if ((int)GroupOf(it.Kind) == g) dst.Add(it);
    }

    private readonly List<ShopItem> _raw = new();

    private void ShopFrame(float dt)
    {
        var t = _drv.Town;
        t.ShopItems(_raw);
        Grouped(_raw, _items);
        if (!SameSet(_items, _shown) || _marks.Count != _items.Count) RebuildMarks();
        _shown.Clear();
        _shown.AddRange(_items);
        if (_markSig.Length != _items.Count) { _markSig = new int[_items.Count]; for (int j = 0; j < _markSig.Length; j++) _markSig[j] = int.MinValue; }
        int offer = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            var b = _marks[i];
            int ord = it.Kind == ShopItemKind.Building ? offer++ : 0;
            // At the last level a step that "unlocks at a later level" never will: show it as finished for this age.
            bool lastLevel = it.Reason == "Unlocks at a later level" && t.Level >= t.MaxLevel;
            bool locked = IsLocked(it) && !lastLevel, done = IsFinished(it) || lastLevel, poor = it.Reason == "Not enough coin";
            // The bar toward the price: only while coin is the one thing missing.
            var bar = _markBars[i];
            bar.Visible = poor && it.Price > 0;
            if (bar.Visible)
            {
                float k = (float)Math.Clamp(t.Coin / it.Price, 0, 1);
                float h = UiKit.Px(_s, 4);
                bar.Position = new Vector2(1, b.Size.Y - h - 1);
                bar.Size = new Vector2(Math.Max(0, (b.Size.X - 2) * k), h);
            }
            // Rebuild the bookmark's text only when something it shows changed (round 2: no strings per frame).
            long need = poor ? (long)Math.Floor(Math.Max(1, it.Price - t.Coin)) : 0;
            bool mode = it.Kind == ShopItemKind.Pave ? PaveMode : it.Kind == ShopItemKind.Extend && ExtendMode;
            int sig = HashCode.Combine((int)it.Kind, it.Level, (long)Math.Floor(it.Price), it.Affordable, it.Reason, need, mode, ord);
            sig = HashCode.Combine(sig, it.Name, b.GetInstanceId(), Settings.GameInput.Version);
            if (sig == _markSig[i]) continue;
            _markSig[i] = sig;
            string key = KeyLabel(ActionFor(it, ord), it.Kind);
            string keyPart = key.Length > 0 ? $"   [{key}]" : "";
            string name = it.Kind switch
            {
                ShopItemKind.Pave => "Pave a road",
                ShopItemKind.Extend => "Extend a cottage",
                _ => it.Name,
            };
            bool track = it.Kind is ShopItemKind.Villager or ShopItemKind.Pace or ShopItemKind.Load or ShopItemKind.Tools or ShopItemKind.Value or ShopItemKind.BellTrack;
            string lvl = track && it.Level > 0 ? $", level {it.Level}" : "";
            string text;
            if (done) text = $"{name}{lvl}\nFinished: {(lastLevel ? "all done this age" : Lower(it.Reason))}";
            else if (locked) text = $"{name}{keyPart}\n{it.Reason}";
            else
            {
                string price = it.Price > 0 && (it.Index >= 0 || it.Kind is not (ShopItemKind.Pave or ShopItemKind.Extend)) ? Money(it.Price) + " coin" : "";
                string state;
                if (mode) state = it.Kind == ShopItemKind.Pave ? $"Pick a road. {StopKey("tt_pave")}" : $"Pick a cottage. {StopKey("tt_buy_5", 1)}";
                else if (it.Affordable) state = it.Kind == ShopItemKind.Pave ? "Choose a road" : it.Kind == ShopItemKind.Extend ? "Choose a cottage" : "Buy";
                else if (poor) state = $"Need {Money(need)} more";
                else state = it.Reason.Length > 0 ? it.Reason : "Not yet";
                text = $"{name}{lvl}{keyPart}\n{(price.Length > 0 ? price + ": " : "")}{state}";
            }
            b.Text = text;
            b.Disabled = !(it.Affordable || mode);
            b.Modulate = locked || done ? new Color(1, 1, 1, 0.75f) : Colors.White;
            b.TooltipText = Tooltip(it);
        }
        // Lay out: bookmarks down the right edge, the bell bottom left, the next age above it.
        var vp = _built;
        _shop.Size = new Vector2(UiKit.Px(_s, 280), 0);
        _shop.Position = new Vector2(vp.X - _shop.Size.X - UiKit.Px(_s, 14), UiKit.Px(_s, 10));
        BellFrame(vp);
    }

    /// <summary>Call when key bindings changed, so the bookmarks print the new keys.</summary>
    /// <summary>Kept for callers: the keys are reprinted by themselves when the bindings change (GameInput.Version).</summary>
    public void KeysChanged() { }

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
        _ => "A building for the town, placed on its plot by the town.",
    };

    private void RebuildMarks()
    {
        foreach (var m in _marks) { _shop.RemoveChild(m); m.QueueFree(); }
        foreach (var h in _heads) { _shop.RemoveChild(h); h.QueueFree(); }
        _marks.Clear();
        _markBars.Clear();
        _heads.Clear();
        int group = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            int g = (int)GroupOf(_items[i].Kind);
            if (g != group)
            {
                group = g;
                var head = UiKit.Text(GroupNames[g], _s, 17, UiKit.Serif);
                head.HorizontalAlignment = HorizontalAlignment.Right;
                head.Name = "Group" + GroupNames[g];
                _shop.AddChild(head);
                _heads.Add(head);
            }
            var b = MakeButton("Mark" + i, IconFor(_items[i].Kind), StandInIcon(_items[i].Kind), 15, 5);
            int k = i;
            b.Pressed += () => Activate(k);
            b.CustomMinimumSize = new Vector2(UiKit.Px(_s, 270), 0);
            var bar = new ColorRect { Name = "Bar", Color = new Color(Palette.Thatch, 0.9f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            b.AddChild(bar);
            _shop.AddChild(b);
            _marks.Add(b);
            _markBars.Add(bar);
        }
    }

    private void BellFrame(Vector2 vp)
    {
        var t = _drv.Town;
        bool unlocked = t.Level >= 2 || t.BellRings > 0 || t.BellReady || t.BellWait > 0;
        int key = !unlocked ? -1 : t.BellRinging ? -2 : t.BellReady ? -3 : (int)Math.Ceiling(t.BellWait);
        if (Settings.GameInput.Version != _bellKeysVersion) { _bellKeysVersion = Settings.GameInput.Version; _bellKey = int.MinValue; }
        if (key != _bellKey)
        {
            _bellKey = key;
            string state = key == -1 ? "Comes at level 2" : key == -2 ? "Ringing: everyone hurries" : key == -3 ? "Ring it" : $"Rests {key} s";
            _bell.Text = $"The bell{Settings.GameInput.Tag("tt_bell", "   ")}\n{state}";
            _bell.TooltipText = "Ring the bell: everyone hurries for a few seconds, then it rests.";
        }
        _bell.Disabled = !t.BellReady;
        _bell.Size = new Vector2(UiKit.Px(_s, 250), 0);
        _bell.ResetSize();
        _bell.Position = new Vector2(UiKit.Px(_s, 16), vp.Y - _bell.Size.Y - UiKit.Px(_s, 16));
        _nextAge.Visible = (t.CanRingNextAge || t.NextAgeRung) && _fullA > 0.01f && !AgePageOpen;
        if (_nextAge.Visible)
        {
            int ns = (t.NextAgeRung ? 1 : 0) + 2 * Settings.GameInput.Version;
            if (ns != _nextAgeSig)
            {
                _nextAgeSig = ns;
                _nextAge.Text = t.NextAgeRung ? $"The first age{Settings.GameInput.Tag("tt_next_age", "   ")}\nRead its page again"
                                              : $"Ring in the next age{Settings.GameInput.Tag("tt_next_age", "   ")}\nThe charter is yours";
            }
            _nextAge.ResetSize();
            _nextAge.Position = new Vector2(UiKit.Px(_s, 16), _bell.Position.Y - _nextAge.Size.Y - UiKit.Px(_s, 8));
        }
    }

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
            else if (!ActivateByKey(e)) handled = false;
            if (handled) GetViewport().SetInputAsHandled();
            return;
        }
        PaveInput(e);
        ExtendInput(e);
    }

    /// <summary>The bookmark whose own key this is, if one is shown.</summary>
    private bool ActivateByKey(InputEvent e)
    {
        int offer = 0;
        for (int i = 0; i < _shown.Count; i++)
        {
            int ord = _shown[i].Kind == ShopItemKind.Building ? offer++ : 0;
            string a = ActionFor(_shown[i], ord);
            if (a.Length > 0 && Is(e, a)) { Activate(i); return true; }
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
