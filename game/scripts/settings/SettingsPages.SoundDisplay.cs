using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>The Sound and Display sections, and the "Keep this?" countdown after a window change.</summary>
public partial class SettingsPages
{
    private static readonly string[] BusLabels = { "Master", "Music", "Ambience", "Effects", "Interface" };
    private static readonly string[] BusNotes =
    {
        "Everything at once.", "The tune of the town.", "Water, wind and the sounds around the town.",
        "Footsteps, bells, coins and building.", "Clicks and page turns.",
    };
    private static readonly string[] ModeNames = { "Windowed", "Borderless full screen", "Full screen" };

    private WindowMode _prevMode, _trialMode;
    private Vector2I _prevRes, _trialRes;
    private float _keepLeft;

    private void BuildSound(VBoxContainer left, VBoxContainer right, float s)
    {
        Heading(left, "Volume", s);
        for (int i = 0; i < GameSettings.Buses.Length; i++)
        {
            string bus = GameSettings.Buses[i];
            // Master, Music and Ambience on the left page; Effects and Interface carry on over the page.
            var col = i < 3 ? left : right;
            if (i == 3) Heading(right, "Volume, continued", s);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", UiKit.Px(s, 12));
            var slider = new VolumeSlider
            {
                Name = "Slider" + bus, MinValue = 0, MaxValue = 100, Step = 1, Value = Store.Volume(bus), FocusMode = FocusModeEnum.All,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
                CustomMinimumSize = new Vector2(0, 30 * s), TooltipText = $"{BusLabels[i]} volume, 0 to 100",
            };
            var value = UiKit.Text(Store.Volume(bus).ToString(), s, 19, UiKit.UiBold);
            value.CustomMinimumSize = new Vector2(44 * s, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            bool dragging = false;
            slider.DragStarted += () => dragging = true;
            slider.DragEnded += changed => { dragging = false; PlayTick(bus); };
            slider.ValueChanged += v =>
            {
                Store.SetVolume(bus, (int)v);
                value.Text = ((int)v).ToString();
                if (!dragging) PlayTick(bus);   // keys and clicks on the rule: a tick at once
            };
            row.AddChild(slider);
            row.AddChild(value);
            Target["sound." + bus] = slider;
            Row(col, BusLabels[i], row, s, BusNotes[i], 300);
        }

        right.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10 * s), MouseFilter = MouseFilterEnum.Ignore });
        Heading(right, "When you look away", s);
        var mute = new SwitchButton(Store.MuteWhenInactive) { TooltipText = "Silence the game while another window is in front" };
        mute.Switched += Store.SetMuteWhenInactive;
        Target["sound.mute"] = mute;
        Row(right, "Mute when the window is not active", mute, s, "The game goes quiet while you use another window.", 120);
        var hint = UiKit.Text("Every change is kept at once. You hear a short coin when you let go of a slider.", s, 16, null, UiKit.FadedInk);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(hint);
    }

    private void BuildDisplay(VBoxContainer left, VBoxContainer right, float s)
    {
        Heading(left, "The window", s);
        // While "Keep this?" is open the choices show what is on trial.
        bool trial = _slip == "keep";
        var shownMode = trial ? _trialMode : Store.WindowMode;
        var shownRes = trial ? _trialRes : Store.Resolution;
        var mode = new ChoiceButton(ModeNames, (int)shownMode, s) { TooltipText = "Windowed, borderless full screen or full screen" };
        mode.Chosen += i => TryDisplay((WindowMode)i, Store.Resolution);
        Target["display.mode"] = mode;
        Row(left, "Window mode", mode, s, null, 290);

        var sizes = Applier.Display.Resolutions(Store.Resolution);
        var names = new string[sizes.Count];
        for (int i = 0; i < sizes.Count; i++) names[i] = $"{sizes[i].X} x {sizes[i].Y}";
        var res = new ChoiceButton(names, Math.Max(0, sizes.IndexOf(shownRes)), s)
        {
            Disabled = shownMode != WindowMode.Windowed, TooltipText = "The size of the window when it is not full screen",
        };
        res.Chosen += i => TryDisplay(Store.WindowMode, sizes[i]);
        Target["display.resolution"] = res;
        Row(left, "Resolution", res, s, shownMode == WindowMode.Windowed ? null : "Used in windowed mode.", 290);

        var vsync = new SwitchButton(Store.Vsync) { TooltipText = "Wait for the screen between frames: no tearing" };
        vsync.Switched += Store.SetVsync;
        Target["display.vsync"] = vsync;
        Row(left, "VSync", vsync, s, "Stops tearing. Turn it off if the game feels slow to answer.", 120);

        var fpsNames = new[] { "30", "60", "120", "None" };
        var fps = new ChoiceButton(fpsNames, Array.IndexOf(GameSettings.FrameLimits, Store.FrameLimit), s)
        { TooltipText = "The most frames drawn each second" };
        fps.Chosen += i => Store.SetFrameLimit(GameSettings.FrameLimits[i]);
        Target["display.fps"] = fps;
        Row(left, "Frame limit", fps, s, null, 160);

        Heading(right, "Reading the page", s);
        var sizeNames = new List<string>();
        for (int p = GameSettings.MinInterface; p <= GameSettings.MaxInterface; p += 10) sizeNames.Add(p + " %");
        var ui = new ChoiceButton(sizeNames.ToArray(), (Store.InterfaceSize - GameSettings.MinInterface) / 10, s)
        { TooltipText = "How large the writing and buttons are" };
        ui.Chosen += i => Store.SetInterfaceSize(GameSettings.MinInterface + 10 * i);
        Target["display.interface"] = ui;
        Row(right, "Interface size", ui, s, "Makes all writing and buttons larger or smaller.", 160);

        var coins = new SwitchButton(Store.ShowCoinNumbers) { TooltipText = "A small number rises where coins are paid" };
        coins.Switched += Store.SetShowCoinNumbers;
        Target["display.coins"] = coins;
        Row(right, "Show coin numbers at deliveries", coins, s, null, 120);

        var follow = new SwitchButton(Store.FollowTown) { TooltipText = "The view moves with the town as it grows, until you move it" };
        follow.Switched += Store.SetFollowTown;
        Target["display.follow"] = follow;
        Row(right, "The view follows the town", follow, s, "Until you move the view yourself.", 120);

        // n10: two levers for a slower graphics card (docs/plans/n10/perf-measures.md: the cost follows the pixels).
        Heading(right, "Drawing", s);
        var detailNames = new string[GameSettings.DrawingDetails.Length];
        for (int i = 0; i < detailNames.Length; i++) detailNames[i] = GameSettings.DrawingDetails[i] + " %";
        var detail = new ChoiceButton(detailNames, Math.Max(0, Array.IndexOf(GameSettings.DrawingDetails, Store.DrawingDetail)), s)
        { TooltipText = "Draws the map with fewer pixels and scales it up; the writing stays sharp" };
        detail.Chosen += i => Store.SetDrawingDetail(GameSettings.DrawingDetails[i]);
        Target["display.detail"] = detail;
        Row(right, "Drawing detail", detail, s, "Lower it if the game runs slowly. The map gets softer.", 160);

        var outline = new ChoiceButton(new[] { "Full", "Light" }, Store.OutlineLight ? 1 : 0, s)
        { TooltipText = "Light: a thinner ink line on large screens, a little faster" };
        outline.Chosen += i => Store.SetOutlineLight(i == 1);
        Target["display.outline"] = outline;
        Row(right, "Outline", outline, s, null, 160);
    }

    /// <summary>Applies a window mode and size on trial and asks "Keep this?"; it goes back by itself after KeepSeconds.</summary>
    private void TryDisplay(WindowMode mode, Vector2I res)
    {
        if (mode == Store.WindowMode && res == Store.Resolution) return;
        _prevMode = Store.WindowMode;
        _prevRes = Store.Resolution;
        _trialMode = mode;
        _trialRes = res;
        _keepLeft = KeepSeconds;
        Applier.Display.ApplyWindow(GetWindow(), mode, res);
        OpenSlip("keep");
    }

    private string KeepText()
    {
        int n = Mathf.CeilToInt(Mathf.Max(0, _keepLeft));
        return n == 1 ? "Going back in 1 second." : $"Going back in {n} seconds.";
    }

    private void KeepDisplay()
    {
        _slip = "";
        Store.SetWindowMode(_trialMode);
        Store.SetResolution(_trialRes);
        Refresh(DisplayAt);
        FocusName(_trialMode != _prevMode ? "display.mode" : "display.resolution");
    }

    /// <summary>Puts the window back as it was before the trial.</summary>
    public void RevertDisplay()
    {
        if (_slip != "keep") return;
        _slip = "";
        Applier.Display.ApplyWindow(GetWindow(), _prevMode, _prevRes);
        Refresh(DisplayAt);
        FocusName(_trialMode != _prevMode ? "display.mode" : "display.resolution");
    }
}
