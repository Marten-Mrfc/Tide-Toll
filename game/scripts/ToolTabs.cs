using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The controls as paper bookmarks sticking out of the map page's outer edge onto the table: time (pause, 1x 2x 4x
/// 8x), tools (fence, footbridge, and with the river rules ferry and drawbridge with their prices, erase), views (flow preview, wear overlay), the book (sound with its volume, reset,
/// new map) and the walker count. Each tab has its SVG icon, a label, its key on a small keycap and a tooltip that
/// says what it does. A tab that is on is pulled further out of the book and drawn with a heavy ink edge, so state
/// never relies on colour. Built for one interface scale; GameUi rebuilds it when the window size changes.
/// </summary>
public partial class ToolTabs : VBoxContainer
{
    public readonly Dictionary<string, Control> Controls = new();
    public Button? Ferry, Drawbridge;
    public Button Pause = null!, Fence = null!, Bridge = null!, Erase = null!, Preview = null!, Overlay = null!, Sound = null!, Reset = null!, NewMap = null!;
    public readonly Button[] Speed = new Button[4];
    public HSlider Walkers = null!, Volume = null!;
    public Label WalkersLabel = null!;
    private TextureRect _pauseIcon = null!, _soundIcon = null!;
    private Label _pauseText = null!;
    private float _s;
    private int _icon;

    public float TabWidth { get; private set; }
    /// <summary>Pixels (at scale 1) of each bookmark that lie under the page edge.</summary>
    public const float Tuck = 9f;

    public static float WidthFor(float s) => 188f * s;

    // Crossing tabs with a price line: the price label, the tooltip without the coin note, and the price.
    private readonly List<(Button tab, Label price, string tip, double cost)> _priced = new();
    private int _shownCoin = int.MinValue;

    public void Build(float s, RiverRules? river = null)
    {
        _s = s;
        _icon = UiKit.Px(s, 24);
        Name = "Tabs";
        TabWidth = WidthFor(s);
        CustomMinimumSize = new Vector2(TabWidth, 0);
        AddThemeConstantOverride("separation", UiKit.Px(s, 4));
        MouseFilter = MouseFilterEnum.Ignore;

        Pause = Tab("Pause", "ui-pause", "Pause", "Space", "Pause or resume time (Space). Tools still work while paused.", true);
        _pauseIcon = Pause.GetNode<TextureRect>("Row/Icon");
        _pauseText = Pause.GetNode<Label>("Row/Text");
        var speeds = new HBoxContainer { Name = "Speeds", MouseFilter = MouseFilterEnum.Ignore };
        speeds.AddThemeConstantOverride("separation", UiKit.Px(s, 3));
        // The speed keys are loose cards, not bookmarks: they start where the page edge ends.
        speeds.AddChild(new Control { Name = "Tucked", CustomMinimumSize = new Vector2(UiKit.Px(s, Tuck + 2), 0), MouseFilter = MouseFilterEnum.Ignore });
        int[] sp = { 1, 2, 4, 8 };
        for (int i = 0; i < 4; i++)
        {
            var b = SmallTab($"Speed{sp[i]}", $"{sp[i]}×", $"Speed {sp[i]}x (key {i + 1})");
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            speeds.AddChild(b);
            Speed[i] = b;
        }
        AddChild(speeds);
        Gap(10);
        Fence = Tab("Fence", "tool-fence", "Fence", "F", "Fence (F): drag across the map to draw a woven fence. Walkers go round it.", true);
        if (river == null)
        {
            Bridge = Tab("Footbridge", "tool-footbridge", "Footbridge", "B", "Footbridge (B): point at the river and click to build a bridge across it.", true);
            Erase = Tab("Erase", "tool-erase", "Erase", "E", "Erase (E): click or drag over fences and bridges to remove them.", true);
        }
        else
        {
            Bridge = Priced("Footbridge", "tool-footbridge", "Footbridge", "B", river.FootbridgePrice,
                "Footbridge (B): a low bridge across the river. Quickest for walkers, but masted barges cannot pass under it and turn back.");
            Ferry = Priced("Ferry", "tool-ferry", "Ferry", "Y", river.FerryPrice,
                $"Ferry (Y): a raft on a rope. Every boat passes; walkers wait for the raft, six at a time. {river.FerryUpkeep + river.FerryWage:0}c a season for the ferryman and upkeep.");
            Drawbridge = Priced("Drawbridge", "tool-drawbridge", "Drawbridge", "D", river.DrawbridgePrice,
                $"Drawbridge (D): walkers cross while it is down; raise it (click it, or L) for barges, or hire a keeper ({river.KeeperPrice:0}c, K) to do it. {river.DrawbridgeUpkeep:0}c a season.");
            Erase = Tab("Erase", "tool-erase", "Erase", "E", "Erase (E): click or drag over fences, bridges and ferries to remove them. Taking down a crossing costs half its price.", true);
        }
        Gap(10);
        Preview = Tab("FlowPreview", "ui-flow-preview", "Flow preview", "P", "Flow preview (P): while you use a tool, show the routes that would change. Dashed is now, solid is after.", true);
        Overlay = Tab("WearOverlay", "ui-wear-overlay", "Wear overlay", "W", "Wear overlay (W): lay a heat map of trampling over the page.", true);
        Gap(10);
        Sound = Tab("Sound", "ui-sound-on", "Sound", "M", "Sound on or off (M).", true);
        _soundIcon = Sound.GetNode<TextureRect>("Row/Icon");
        Volume = Slider("Volume", 0, 100, 5, "Volume of all sound", UiKit.Text("Volume", _s, 14));
        Reset = Tab("Reset", "ui-reset", "Reset", "R", "Reset (R): start this map again from the first day.", false);
        NewMap = Tab("NewMap", "ui-new-map", "New map", "N", "New map (N): a new town from the next seed.", false);
        Gap(10);
        WalkersLabel = UiKit.Text("Walkers 200", s, 15, UiKit.UiBold);
        WalkersLabel.Name = "WalkersLabel";
        Walkers = Slider("Walkers", 50, 500, 10, "How many villagers live here (50 to 500). Drag, or focus and use the arrow keys.", WalkersLabel);
    }

    /// <summary>A tool tab with its price on a second line, greyed with the shortfall when the purse cannot pay it.</summary>
    private Button Priced(string name, string icon, string text, string key, double cost, string tip)
    {
        var b = Tab(name, icon, text, key, tip, true, 48);
        var label = b.GetNode<Label>("Row/Text");
        var row = (HBoxContainer)label.GetParent();
        int at = label.GetIndex();
        row.RemoveChild(label);
        var lines = new VBoxContainer { Name = "Lines", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        lines.AddThemeConstantOverride("separation", 0);
        lines.AddChild(label);
        var price = UiKit.Text($"{cost:0}c", _s, 13, UiKit.UiBold);
        price.Name = "Price";
        lines.AddChild(price);
        row.AddChild(lines);
        row.MoveChild(lines, at);
        label.Name = "Text";
        _priced.Add((b, price, tip, cost));
        return b;
    }

    /// <summary>Prices in ink when the purse can pay them, greyed with what is missing when not (and the tooltip says so).</summary>
    public void ShowPrices(double money)
    {
        int coin = (int)System.Math.Floor(money + 1e-9);
        if (coin == _shownCoin) return;
        _shownCoin = coin;
        foreach (var (tab, price, tip, cost) in _priced)
        {
            bool ok = money >= cost - 1e-9;
            price.Text = ok ? $"{cost:0}c" : $"{cost:0}c · short {System.Math.Ceiling(cost - money):0}c";
            price.AddThemeColorOverride("font_color", ok ? UiKit.Ink : UiKit.FadedInk);
            tab.TooltipText = ok ? tip : $"{tip}\nNot enough coin: it costs {cost:0}c, you have {coin}c.";
        }
    }

    /// <summary>Whether a priced tab shows its price as affordable (tests).</summary>
    public bool PriceAffordable(string name)
    {
        foreach (var p in _priced) if (p.tab.Name == name) return !p.price.Text.Contains("short");
        return false;
    }

    private void Gap(float px) => AddChild(new Control { CustomMinimumSize = new Vector2(0, UiKit.Px(_s, px)), MouseFilter = MouseFilterEnum.Ignore });

    private Button Tab(string name, string icon, string text, string key, string tip, bool toggle, float height = 40)
    {
        var b = new Button { Name = name, ToggleMode = toggle, TooltipText = tip, FocusMode = FocusModeEnum.All, ActionMode = BaseButton.ActionModeEnum.Release };
        var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiKit.Px(_s, 7));
        var tr = new TextureRect
        {
            Name = "Icon", Texture = UiKit.Icon(icon, _icon), CustomMinimumSize = new Vector2(_icon, _icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        };
        row.AddChild(tr);
        var label = UiKit.Text(text, _s, 16);
        label.Name = "Text";
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        row.AddChild(Keycap(key));
        Place(b, row, height);
        AddChild(b);
        Controls[name] = b;
        return b;
    }

    private Button SmallTab(string name, string text, string tip)
    {
        var b = new Button { Name = name, ToggleMode = true, TooltipText = tip, FocusMode = FocusModeEnum.All, ActionMode = BaseButton.ActionModeEnum.Release };
        var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        var label = UiKit.Text(text, _s, 15, UiKit.UiBold);
        label.Name = "Text";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(label);
        Place(b, row, 32);
        b.AddThemeStyleboxOverride("normal", UiKit.Card(_s, UiKit.Paper, 1.5f));
        b.AddThemeStyleboxOverride("hover", UiKit.Card(_s, UiKit.PaperHover, 1.5f));
        var on = UiKit.Card(_s, UiKit.Pressed, 3f);
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        Controls[name] = b;
        return b;
    }

    // The row fills the button (a Button does not lay out children itself).
    private void Place(Button b, Control row, float height)
    {
        b.CustomMinimumSize = new Vector2(0, UiKit.Px(_s, height));
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = UiKit.Px(_s, 10 + Tuck); row.OffsetRight = -UiKit.Px(_s, 8);
        row.OffsetTop = UiKit.Px(_s, 3); row.OffsetBottom = -UiKit.Px(_s, 3);
        b.AddChild(row);
    }

    private Label Keycap(string key)
    {
        var l = UiKit.Text(key, _s, 12, UiKit.UiBold, UiKit.FadedInk.Darkened(0.2f));
        l.Name = "Key";
        var box = new StyleBoxFlat { BgColor = new Color(UiKit.Paper, 0), BorderColor = UiKit.FadedInk, AntiAliasing = true };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(UiKit.Px(_s, 3));
        box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(_s, 4);
        l.AddThemeStyleboxOverride("normal", box);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return l;
    }

    /// <summary>A slider on a small paper card with its caption above it.</summary>
    private HSlider Slider(string name, double min, double max, double step, string tip, Label caption)
    {
        var sl = new HSlider
        {
            Name = name, MinValue = min, MaxValue = max, Step = step, TooltipText = tip, FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 24)),
        };
        var card = new PanelContainer { Name = name + "Card", MouseFilter = MouseFilterEnum.Stop };
        var style = UiKit.Tab(_s, UiKit.Paper, 1.5f, 0);
        style.ContentMarginTop = style.ContentMarginBottom = UiKit.Px(_s, 4);
        card.AddThemeStyleboxOverride("panel", style);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(caption);
        box.AddChild(sl);
        card.AddChild(box);
        AddChild(card);
        Controls[name] = sl;
        return sl;
    }

    /// <summary>Pause tab text and icon follow the state (Pause while running, Play while paused).</summary>
    public void ShowPaused(bool paused)
    {
        Pause.SetPressedNoSignal(paused);
        _pauseIcon.Texture = UiKit.Icon(paused ? "ui-play" : "ui-pause", _icon);
        _pauseText.Text = paused ? "Play" : "Pause";
    }

    public void ShowSound(bool on)
    {
        Sound.SetPressedNoSignal(on);
        _soundIcon.Texture = UiKit.Icon(on ? "ui-sound-on" : "ui-sound-off", _icon);
    }

    /// <summary>
    /// The page's outer edge drawn over the bookmarks' tucked-in ends: a strip of page paper with the edge's ink rule
    /// and a stack line, and a soft shadow the page casts on each tab. Only over the tabs' rows; nothing when the tabs
    /// lie loose on the page (a narrow window). Redrawn only when the edge or the tabs move.
    /// </summary>
    public partial class PageLip : Control
    {
        public ToolTabs Tabs = null!;
        public float Scale1 = 1;
        private float _edge = float.NaN;
        private bool _on;
        private Vector2 _tabsAt, _tabsSize;

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public void Place(float edgeX, bool on)
        {
            if (edgeX == _edge && on == _on && Tabs.Position == _tabsAt && Tabs.Size == _tabsSize) return;
            _edge = edgeX; _on = on; _tabsAt = Tabs.Position; _tabsSize = Tabs.Size;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (!_on || float.IsNaN(_edge)) return;
            float s = Scale1, x0 = Tabs.Position.X - 1, x1 = Mathf.Round(_edge);
            if (x1 <= x0) return;
            var paper = UiKit.Paper;
            var stack = UiKit.Paper.Darkened(0.12f);
            foreach (var c in Tabs.GetChildren())
            {
                if (c is not (Button or PanelContainer)) continue;
                var r = ((Control)c).GetRect();
                float y0 = Tabs.Position.Y + r.Position.Y - 1, h = r.Size.Y + 2;
                DrawRect(new Rect2(x0, y0, x1 - x0, h), paper);
                DrawRect(new Rect2(x1 - 3 * s, y0, Mathf.Max(1, s), h), stack);        // a page of the stack
                for (int k = 0; k < 4; k++)                                             // the page's soft shadow
                    DrawRect(new Rect2(x1 + k * s, y0 + 1, s, h - 2), new Color(UiKit.Ink, 0.16f * (1 - k / 4f)));
                DrawRect(new Rect2(x1 - Mathf.Max(1, s), y0, Mathf.Max(1, 1.2f * s), h), UiKit.Ink); // the edge rule
            }
        }
    }
}
