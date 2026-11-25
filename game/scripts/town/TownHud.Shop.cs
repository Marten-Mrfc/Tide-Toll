using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The shop side of the interface: a bookmark per shop item down the right edge (name, level, price, and either
/// "Buy" with its key or the reason it cannot be bought yet), the bell at the bottom left with its rest time, and the
/// charter's "Ring in the next age". The Pave bookmark switches Pave mode (TownHud.Pave.cs) instead of buying.
/// </summary>
public partial class TownHud
{
    private VBoxContainer _shop = null!;
    private readonly List<Button> _marks = new();
    private readonly List<ShopItem> _items = new();
    private readonly List<ShopItem> _shown = new();
    private Button _bell = null!, _nextAge = null!;
    private float _shopClock;

    /// <summary>The bookmark button for a shop item kind (first one; tests), or null.</summary>
    public Button? Mark(ShopItemKind kind)
    {
        for (int i = 0; i < _shown.Count && i < _marks.Count; i++) if (_shown[i].Kind == kind) return _marks[i];
        return null;
    }
    public IReadOnlyList<ShopItem> ShownItems => _shown;
    public Button BellButton => _bell;
    public Button NextAgeButton => _nextAge;

    public static string KeyFor(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "1", ShopItemKind.Pace => "2", ShopItemKind.Load => "3", ShopItemKind.Pave => "4 or P", ShopItemKind.Building => "5",
        _ => "",
    };

    private static string IconFor(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "up-villager", ShopItemKind.Pace => "up-pace", ShopItemKind.Load => "up-load",
        ShopItemKind.Pave => "up-pave", _ => "up-building",
    };

    private static string StandInIcon(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "ui-reputation", ShopItemKind.Pace => "ui-speed", ShopItemKind.Load => "ui-boat",
        ShopItemKind.Pave => "tool-gravel", _ => "ui-new-map",
    };

    private void BuildShop()
    {
        _shop = new VBoxContainer { Name = "Shop", MouseFilter = Control.MouseFilterEnum.Ignore };
        _shop.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        _root.AddChild(_shop);
        var head = UiKit.Text("Upgrades", _s, 20, UiKit.Serif);
        head.HorizontalAlignment = HorizontalAlignment.Right;
        _shop.AddChild(head);
        _marks.Clear();
        _shown.Clear();

        _bell = MakeButton("Bell", "ui-bell", "ui-sound-on");
        _bell.Pressed += RingBell;
        _root.AddChild(_bell);
        _nextAge = MakeButton("NextAge", "ui-achievement", "ui-reputation");
        _nextAge.Pressed += () => _drv.Town.RingNextAge();
        _nextAge.Visible = false;
        _root.AddChild(_nextAge);
    }

    private Button MakeButton(string name, string icon, string standIn)
    {
        var b = new Button
        {
            Name = name, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None,
            Icon = IconOr(icon, standIn), ExpandIcon = false, ClipText = false,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        b.AddThemeConstantOverride("icon_max_width", UiKit.Px(_s, 30));
        b.AddThemeConstantOverride("h_separation", UiKit.Px(_s, 10));
        b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, 16));
        // Bookmarks: paper slips with an ink edge; a slip that can be bought now is pulled out a little and brighter.
        var normal = UiKit.Card(_s, UiKit.Paper, 1.5f);
        normal.SetContentMarginAll(UiKit.Px(_s, 8));
        var hover = UiKit.Card(_s, UiKit.PaperHover, 2f);
        hover.SetContentMarginAll(UiKit.Px(_s, 8));
        var pressed = UiKit.Card(_s, UiKit.Pressed, 2.5f);
        pressed.SetContentMarginAll(UiKit.Px(_s, 8));
        var off = UiKit.Card(_s, UiKit.PaperDark, 1f);
        off.SetContentMarginAll(UiKit.Px(_s, 8));
        off.BorderColor = UiKit.FadedInk;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", off);
        b.AddThemeColorOverride("font_disabled_color", UiKit.Ink.Lerp(UiKit.Paper, 0.25f));
        return b;
    }

    private void RingBell()
    {
        if (!_drv.Town.RingBell()) Note(_drv.Town.Level < 2 ? "The bell comes at level 2." : $"The bell is resting: {Math.Ceiling(_drv.Town.BellWait)} s.");
    }

    /// <summary>Buys the shown item at a bookmark (the Pave bookmark toggles Pave mode instead).</summary>
    public void Activate(int i)
    {
        if (i < 0 || i >= _shown.Count) return;
        var it = _shown[i];
        if (it.Kind == ShopItemKind.Pave) { SetPaveMode(!PaveMode); return; }
        // Read the item afresh: prices change after every purchase.
        _drv.Town.ShopItems(_items);
        foreach (var cur in _items)
            if (cur.Kind == it.Kind && cur.Index == it.Index)
            {
                if (!_drv.Town.Buy(cur)) Note(cur.Reason.Length > 0 ? cur.Reason + "." : "Not yet.");
                return;
            }
    }

    private void ActivateKind(ShopItemKind k)
    {
        for (int i = 0; i < _shown.Count; i++) if (_shown[i].Kind == k) { Activate(i); return; }
    }

    private static bool SameSet(List<ShopItem> a, List<ShopItem> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i].Kind != b[i].Kind || a[i].Index != b[i].Index) return false;
        return true;
    }

    private void ShopFrame(float dt)
    {
        var t = _drv.Town;
        t.ShopItems(_items);
        if (!SameSet(_items, _shown) || _marks.Count != _items.Count)
        {
            foreach (var m in _marks) { _shop.RemoveChild(m); m.QueueFree(); }
            _marks.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var b = MakeButton("Mark" + i, IconFor(_items[i].Kind), StandInIcon(_items[i].Kind));
                int k = i;
                b.Pressed += () => Activate(k);
                b.CustomMinimumSize = new Vector2(UiKit.Px(_s, 270), 0);
                _shop.AddChild(b);
                _marks.Add(b);
            }
        }
        _shown.Clear();
        _shown.AddRange(_items);
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            var b = _marks[i];
            string lvl = it.Kind is ShopItemKind.Villager or ShopItemKind.Pace or ShopItemKind.Load ? $", level {it.Level}" : "";
            string price = it.Kind == ShopItemKind.Pave && it.Index < 0 ? "" : Money(it.Price) + " coin";
            string state;
            if (it.Kind == ShopItemKind.Pave)
                state = it.Reason.Length > 0 && it.Reason != "Not enough coin" ? it.Reason : PaveMode ? "Pick a road. P to stop" : "Pave a road";
            else if (it.Affordable) state = "Buy";
            else if (it.Reason == "Not enough coin") state = $"Need {Money(Math.Max(1, it.Price - t.Coin))} more";
            else state = it.Reason.Length > 0 ? it.Reason : "Not yet";
            string name = it.Kind == ShopItemKind.Pave ? "Pave" : it.Name;
            b.Text = $"{name}{lvl}   [{KeyFor(it.Kind)}]\n{(price.Length > 0 ? price + ": " : "")}{state}";
            bool can = it.Kind == ShopItemKind.Pave ? (it.Reason.Length == 0 || it.Reason == "Not enough coin" || PaveMode) : it.Affordable;
            b.Disabled = !can;
            b.TooltipText = it.Kind switch
            {
                ShopItemKind.Villager => "One more villager. Each cottage houses two.",
                ShopItemKind.Pace => "Everyone walks a little faster.",
                ShopItemKind.Load => "Everyone carries one more unit per trip.",
                ShopItemKind.Pave => "Pave one worn-in road: faster, and it carries more people. Click a road on the map.",
                _ => "A building for the town, placed on its plot by the town.",
            };
        }
        // Lay out: bookmarks down the right edge, the bell bottom left, the next age above it.
        var vp = _built;
        _shop.Size = new Vector2(UiKit.Px(_s, 280), 0);
        _shop.Position = new Vector2(vp.X - _shop.Size.X - UiKit.Px(_s, 14), UiKit.Px(_s, 14));
        BellFrame(vp);
    }

    private void BellFrame(Vector2 vp)
    {
        var t = _drv.Town;
        bool unlocked = t.Level >= 2 || t.BellRings > 0 || t.BellReady || t.BellWait > 0;
        string state = !unlocked ? "Comes at level 2" : t.BellRinging ? "Ringing: everyone hurries" : t.BellReady ? "Ring it" : $"Rests {Math.Ceiling(t.BellWait)} s";
        _bell.Text = $"The bell   [Space]\n{state}";
        _bell.Disabled = !t.BellReady;
        _bell.TooltipText = "Ring the bell: everyone hurries for a few seconds, then it rests.";
        _bell.Size = new Vector2(UiKit.Px(_s, 250), 0);
        _bell.ResetSize();
        _bell.Position = new Vector2(UiKit.Px(_s, 16), vp.Y - _bell.Size.Y - UiKit.Px(_s, 16));
        _nextAge.Visible = t.CanRingNextAge;
        if (_nextAge.Visible)
        {
            _nextAge.Text = "Ring in the next age   [N]\nThe charter is yours";
            _nextAge.ResetSize();
            _nextAge.Position = new Vector2(UiKit.Px(_s, 16), _bell.Position.Y - _nextAge.Size.Y - UiKit.Px(_s, 8));
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            bool handled = true;
            if (Is(e, "tt_buy_1")) ActivateKind(ShopItemKind.Villager);
            else if (Is(e, "tt_buy_2")) ActivateKind(ShopItemKind.Pace);
            else if (Is(e, "tt_buy_3")) ActivateKind(ShopItemKind.Load);
            else if (Is(e, "tt_buy_4") || Is(e, "tt_pave")) SetPaveMode(!PaveMode);
            else if (Is(e, "tt_buy_5")) ActivateKind(ShopItemKind.Building);
            else if (Is(e, "tt_bell")) RingBell();
            else if (k.Keycode == Key.N && _drv.Town.CanRingNextAge) _drv.Town.RingNextAge();
            else if (k.Keycode == Key.Escape && PaveMode) SetPaveMode(false);
            else handled = false;
            if (handled) GetViewport().SetInputAsHandled();
            return;
        }
        PaveInput(e);
    }

    private static bool Is(InputEvent e, string action) => InputMap.HasAction(action) && e.IsActionPressed(action);
}
