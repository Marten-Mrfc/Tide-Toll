using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// A badge by every drawbridge on the map (UX tab, bridge badge): a paper card pinned above the bridge with the state
/// in words and as a glyph with a progress ring (down, raising, up, lowering), the boats waiting below it (hull icon and
/// number) and the walkers waiting on each bank (west and east), the lever (Raise or Lower, key L) and the keeper
/// (Hire keeper with its price, key K; greyed with the reason when the purse is short; "Keeper on duty" once hired).
/// The selected bridge's badge has the sky-blue focus frame. Badges follow their bridge on screen every frame and hide
/// when it is off screen; their text changes only when a value does. Rebuilt when crossings are built or removed.
/// </summary>
public partial class BridgeBadges : Control
{
    private sealed class Badge
    {
        public int Id;
        public PanelContainer Card = null!;
        public Label State = null!, Boats = null!, Walkers = null!;
        public Button Lever = null!, Keeper = null!;
        public StateGlyph Glyph = null!;
        public Vector3 Anchor;
        public BridgeState Shown = (BridgeState)255;
        public int ShownBoats = -1, ShownW = -1, ShownE = -1, ShownCoin = int.MinValue;
        public bool ShownKeeper, ShownSelected;
    }

    private Main _main = null!;
    private float _s;
    private readonly List<Badge> _badges = new();
    private StyleBoxFlat _card = null!, _cardSelected = null!;

    public int Count => _badges.Count;

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "BridgeBadges";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _card = UiKit.Card(s, UiKit.Paper, 1.5f);
        _card.SetContentMarginAll(UiKit.Px(s, 6));
        _cardSelected = UiKit.Card(s, UiKit.Paper, 1.5f);
        _cardSelected.SetContentMarginAll(UiKit.Px(s, 6));
        _cardSelected.BorderColor = UiKit.Focus;
        _cardSelected.SetBorderWidthAll(Mathf.Max(2, UiKit.Px(s, 3)));
        main.Sim.Edited += OnEdited;
        main.Tools.SelectionChanged += OnSelection;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_main == null) return;
        _main.Sim.Edited -= OnEdited;
        _main.Tools.SelectionChanged -= OnSelection;
    }

    private void OnEdited(int v) => Rebuild();
    private void OnSelection() { foreach (var b in _badges) b.ShownSelected = !b.ShownSelected; } // forces a restyle

    /// <summary>The badge's lever and keeper buttons for a drawbridge (tests).</summary>
    public Button? LeverButton(int id) { foreach (var b in _badges) if (b.Id == id) return b.Lever; return null; }
    public Button? KeeperButton(int id) { foreach (var b in _badges) if (b.Id == id) return b.Keeper; return null; }
    public Control? Card(int id) { foreach (var b in _badges) if (b.Id == id) return b.Card; return null; }
    public string StateText(int id) { foreach (var b in _badges) if (b.Id == id) return b.State.Text; return ""; }

    public void Rebuild()
    {
        foreach (var b in _badges) { RemoveChild(b.Card); b.Card.QueueFree(); }
        _badges.Clear();
        var w = _main.Sim.World;
        var r = w.River;
        if (r == null) return;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width, k = 0;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            if (x.Type != CrossingType.Drawbridge) continue;
            k++;
            var b = MakeBadge(x.Id, k, r.Rules);
            // Pinned over the lift's gantry, on the pier's (west) bank.
            b.Anchor = new Vector3((x.Cells[0] % n) * cell, 8f, (x.Row + 0.5f) * cell);
            _badges.Add(b);
            AddChild(b.Card);
        }
        Refresh(true);
    }

    private Badge MakeBadge(int id, int number, RiverRules rules)
    {
        var b = new Badge { Id = id };
        b.Card = new PanelContainer { Name = $"Bridge{id}", MouseFilter = MouseFilterEnum.Stop, Visible = false };
        b.Card.AddThemeStyleboxOverride("panel", _card);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 2));
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        b.Glyph = new StateGlyph { CustomMinimumSize = new Vector2(UiKit.Px(_s, 26), UiKit.Px(_s, 26)), Scale1 = _s };
        top.AddChild(b.Glyph);
        b.State = UiKit.Text($"Drawbridge {number}", _s, 14, UiKit.UiBold);
        b.State.Name = "State";
        b.State.VerticalAlignment = VerticalAlignment.Center;
        top.AddChild(b.State);
        col.AddChild(top);
        var counts = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        counts.AddThemeConstantOverride("separation", UiKit.Px(_s, 4));
        counts.AddChild(Icon("ui-boat", 16));
        b.Boats = UiKit.Text("0", _s, 13, UiKit.UiBold);
        b.Boats.Name = "Boats";
        counts.AddChild(b.Boats);
        b.Walkers = UiKit.Text("", _s, 13);
        b.Walkers.Name = "Walkers";
        counts.AddChild(b.Walkers);
        col.AddChild(counts);
        var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        buttons.AddThemeConstantOverride("separation", UiKit.Px(_s, 4));
        b.Lever = SmallButton("Lever", "ui-raise-bridge", "Raise  L", $"Raise or lower drawbridge {number} by hand (L for the selected bridge, Ctrl+{number} for this one). Barges pass only while it is up; walkers wait at the barriers.");
        b.Lever.Pressed += () => { _main.Tools.Select(id); _main.Tools.PullLever(id); };
        buttons.AddChild(b.Lever);
        b.Keeper = SmallButton("Keeper", "ui-hire-keeper", $"Keeper {rules.KeeperPrice:0}c  K", $"Hire a keeper ({rules.KeeperPrice:0}c, then {rules.KeeperWage:0}c a season): raises the bridge for each barge and lowers it once it is through.");
        b.Keeper.Pressed += () => { _main.Tools.Select(id); _main.Tools.HireKeeper(id); };
        buttons.AddChild(b.Keeper);
        col.AddChild(buttons);
        b.Card.AddChild(col);
        return b;
    }

    private TextureRect Icon(string name, float px) => new()
    {
        Texture = UiKit.Icon(name, UiKit.Px(_s, px)), CustomMinimumSize = new Vector2(UiKit.Px(_s, px), UiKit.Px(_s, px)),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps, SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };

    private Button SmallButton(string name, string icon, string text, string tip)
    {
        var b = new Button { Name = name, TooltipText = tip, FocusMode = FocusModeEnum.All, ActionMode = BaseButton.ActionModeEnum.Release, Text = text, Icon = UiKit.Icon(icon, UiKit.Px(_s, 18)), ExpandIcon = true };
        b.AddThemeConstantOverride("icon_max_width", UiKit.Px(_s, 18));
        b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(_s, 13));
        b.AddThemeStyleboxOverride("normal", UiKit.Card(_s, UiKit.Paper, 1.2f));
        b.AddThemeStyleboxOverride("hover", UiKit.Card(_s, UiKit.PaperHover, 1.2f));
        b.AddThemeStyleboxOverride("pressed", UiKit.Card(_s, UiKit.Pressed, 2f));
        b.AddThemeStyleboxOverride("disabled", UiKit.Card(_s, UiKit.PaperDark, 1f));
        foreach (var sb in new[] { "normal", "hover", "pressed", "disabled" })
            ((StyleBoxFlat)b.GetThemeStylebox(sb)).SetContentMarginAll(UiKit.Px(_s, 4));
        return b;
    }

    public override void _Process(double delta) => Refresh(false);

    /// <summary>Places every badge over its bridge and updates what changed. Allocation-free unless a value changed.</summary>
    private void Refresh(bool force)
    {
        if (_badges.Count == 0) return;
        var w = _main.Sim.World;
        var r = w.River;
        if (r == null) return;
        var vp = GetViewportRect().Size;
        var xs = r.Crossings;
        int coin = (int)Math.Floor(r.Money + 1e-9);
        foreach (var b in _badges)
        {
            Crossing? x = null;
            for (int i = 0; i < xs.Count; i++) if (xs[i].Id == b.Id) { x = xs[i]; break; }
            if (x == null) continue;
            var at = _main.Rig.LocalToScreen(b.Anchor);
            var size = b.Card.Size;
            var pos = new Vector2(Mathf.Round(at.X - size.X / 2f), Mathf.Round(at.Y - size.Y - 10 * _s));
            bool on = at.X > 0 && at.Y > 0 && at.X < vp.X && at.Y < vp.Y && _main.Rig.Camera.Size < 420f;
            if (b.Card.Visible != on) b.Card.Visible = on;
            if (b.Card.Position != pos) b.Card.Position = pos;
            bool selected = _main.Tools.SelectedBridge == b.Id;
            if (force || selected != b.ShownSelected)
            {
                b.ShownSelected = selected;
                b.Card.AddThemeStyleboxOverride("panel", selected ? _cardSelected : _card);
            }
            if (force || x.State != b.Shown)
            {
                b.Shown = x.State;
                b.State.Text = x.State switch
                {
                    BridgeState.Closed => "Down: walkers cross",
                    BridgeState.Raising => "Raising…",
                    BridgeState.Raised => "Up: boats pass",
                    _ => "Lowering…",
                };
                bool raise = x.State is BridgeState.Closed or BridgeState.Lowering;
                b.Lever.Text = raise ? "Raise  L" : "Lower  L";
                b.Lever.Icon = UiKit.Icon(raise ? "ui-raise-bridge" : "ui-lower-bridge", UiKit.Px(_s, 18));
                b.Glyph.State = x.State;
                b.Card.ResetSize();
            }
            if (b.Glyph.Progress != (float)x.Progress) { b.Glyph.Progress = (float)x.Progress; b.Glyph.QueueRedraw(); }
            if (force || x.BoatsWaiting != b.ShownBoats)
            {
                b.ShownBoats = x.BoatsWaiting;
                b.Boats.Text = x.BoatsWaiting == 1 ? "1 boat waiting" : $"{x.BoatsWaiting} boats waiting";
            }
            if (force || x.QueueWest != b.ShownW || x.QueueEast != b.ShownE)
            {
                b.ShownW = x.QueueWest; b.ShownE = x.QueueEast;
                b.Walkers.Text = $" · walkers W {x.QueueWest}, E {x.QueueEast}";
            }
            if (force || x.Keeper != b.ShownKeeper || coin != b.ShownCoin)
            {
                b.ShownKeeper = x.Keeper; b.ShownCoin = coin;
                double price = r.Rules.KeeperPrice;
                b.Keeper.Disabled = x.Keeper;
                b.Keeper.Text = x.Keeper ? "Keeper on duty" : coin >= price ? $"Keeper {price:0}c  K" : $"Keeper {price:0}c · short {Math.Ceiling(price - r.Money):0}c";
                b.Keeper.Modulate = x.Keeper || coin >= price ? Colors.White : new Color(1, 1, 1, 0.6f);
            }
        }
    }

    /// <summary>The bridge's state as a shape: a flat deck (down), a deck at an angle with a progress ring (moving), an upright leaf (up).</summary>
    public partial class StateGlyph : Control
    {
        public BridgeState State;
        public float Progress, Scale1 = 1;

        public override void _Draw()
        {
            float w = Size.X, h = Size.Y, s = Scale1, lw = Mathf.Max(1.5f, 2.2f * s);
            var c = new Vector2(w / 2f, h / 2f);
            float r = Mathf.Min(w, h) / 2f - lw;
            DrawCircle(c, r + lw * 0.5f, UiKit.Paper);
            DrawArc(c, r, 0, Mathf.Tau, 24, UiKit.FadedInk, Mathf.Max(1f, s));
            bool moving = State is BridgeState.Raising or BridgeState.Lowering;
            if (moving) DrawArc(c, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Progress, 24, UiKit.Warn, lw * 1.4f);
            // The leaf from its hinge at the lower left, at the drawn angle (flat = down, upright = up).
            float a = Mathf.DegToRad(80f) * Progress;
            var hinge = c + new Vector2(-r * 0.55f, r * 0.35f);
            var tip = hinge + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * r * 1.1f;
            DrawLine(hinge, tip, UiKit.Ink, lw * 1.3f);
            DrawLine(c + new Vector2(-r * 0.8f, r * 0.35f), hinge, UiKit.Ink, lw);
        }
    }
}
