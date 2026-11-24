using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The controls as paper bookmarks sticking out of the map page's outer edge onto the table: time (pause, 1x 2x 4x
/// 8x), tools (fence, footbridge, erase), views (flow preview, wear overlay), the book (sound with its volume, reset,
/// new map) and the walker count. Each tab has its SVG icon, a label, its key on a small keycap and a tooltip that
/// says what it does. A tab that is on is pulled further out of the book and drawn with a heavy ink edge, so state
/// never relies on colour. Built for one interface scale; GameUi rebuilds it when the window size changes.
/// </summary>
public partial class ToolTabs : VBoxContainer
{
    public readonly Dictionary<string, Control> Controls = new();
    public Button Pause = null!, Fence = null!, Bridge = null!, Erase = null!, Preview = null!, Overlay = null!, Sound = null!, Reset = null!, NewMap = null!;
    public readonly Button[] Speed = new Button[4];
    public HSlider Walkers = null!, Volume = null!;
    public Label WalkersLabel = null!;
    private TextureRect _pauseIcon = null!, _soundIcon = null!;
    private Label _pauseText = null!;
    private float _s;
    private int _icon;

    public float TabWidth { get; private set; }

    public static float WidthFor(float s) => 188f * s;

    public void Build(float s)
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
        Bridge = Tab("Footbridge", "tool-footbridge", "Footbridge", "B", "Footbridge (B): point at the river and click to build a bridge across it.", true);
        Erase = Tab("Erase", "tool-erase", "Erase", "E", "Erase (E): click or drag over fences and bridges to remove them.", true);
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

    private void Gap(float px) => AddChild(new Control { CustomMinimumSize = new Vector2(0, UiKit.Px(_s, px)), MouseFilter = MouseFilterEnum.Ignore });

    private Button Tab(string name, string icon, string text, string key, string tip, bool toggle)
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
        Place(b, row, 40);
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
        row.OffsetLeft = UiKit.Px(_s, 10); row.OffsetRight = -UiKit.Px(_s, 8);
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
}
