using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the interface: every control exists, is on screen, has a tooltip and a visible label, and does
/// what it says when clicked with real mouse events (pause, the four speeds, the three tools, flow preview, wear
/// overlay, sound, volume, reset, new map, the walker slider); every keyboard shortcut works; Tab moves a visible
/// keyboard focus; the chapter page shows the village facts and path health; no interface text is smaller than
/// 12 px at 1280 x 720; and at 1920 x 1080 the interface is rebuilt at full scale.
/// Prints UI PASS or UI FAIL and quits with 0 or 1.
/// </summary>
public partial class UiTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();
    private static readonly string[] Names =
    {
        "Pause", "Speed1", "Speed2", "Speed4", "Speed8", "Fence", "Footbridge", "Erase", "FlowPreview", "WearOverlay",
        "Sound", "Volume", "Reset", "NewMap", "Walkers",
    };

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            await TestInput.Frames(this, 4);
            Controls();
            await Clicks();
            await Keys();
            await Focus();
            await Page();
            await Resize();
            GD.Print("UI PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("UI FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private Control C(string name) => _main.Ui.Controls[name];

    private void Controls()
    {
        var vp = GetViewport().GetVisibleRect();
        foreach (var n in Names)
        {
            Check(_main.Ui.Controls.TryGetValue(n, out var c), $"control {n} missing");
            Check(c!.IsVisibleInTree(), $"{n} hidden");
            Check(vp.Encloses(c.GetGlobalRect()), $"{n} not fully on screen: {c.GetGlobalRect()}");
            Check(c.TooltipText.Length > 10, $"{n} has no tooltip");
            Check(c.FocusMode == Control.FocusModeEnum.All, $"{n} cannot take keyboard focus");
            if (c is Button b)
            {
                var label = b.GetNodeOrNull<Label>("Row/Text");
                Check(label != null && label.Text.Length > 0, $"{n} has no label");
            }
        }
        _notes.Add($"controls={Names.Length}");
    }

    private async Task Click(Control c)
    {
        var p = TestInput.Centre(c);
        TestInput.Motion(p);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
    }

    private async Task Clicks()
    {
        var sim = _main.Sim;
        bool paused = sim.Paused;
        await Click(C("Pause"));
        Check(sim.Paused != paused, "Pause tab did not pause");
        Check(((Button)C("Pause")).ButtonPressed == sim.Paused, "Pause tab does not show the state");
        await Click(C("Pause"));
        Check(sim.Paused == paused, "Pause tab did not resume");
        foreach (int s in new[] { 4, 8, 2, 1 })
        {
            await Click(C($"Speed{s}"));
            Check(sim.Speed == s, $"Speed{s} tab set speed {sim.Speed}");
            Check(((Button)C($"Speed{s}")).ButtonPressed, $"Speed{s} tab not shown as on");
        }
        await Click(C("Speed1"));
        Check(sim.Speed == 1 && ((Button)C("Speed1")).ButtonPressed, "clicking the current speed turned it off");
        (string, Tool)[] tools = { ("Fence", Tool.Fence), ("Footbridge", Tool.Bridge), ("Erase", Tool.Erase) };
        foreach (var (n, t) in tools)
        {
            await Click(C(n));
            Check(_main.Tools.Tool == t && ((Button)C(n)).ButtonPressed, $"{n} tab did not pick up the tool");
            await Click(C(n));
            Check(_main.Tools.Tool == Tool.None && !((Button)C(n)).ButtonPressed, $"{n} tab did not put the tool down");
        }
        bool prev = _main.Tools.PreviewEnabled;
        await Click(C("FlowPreview"));
        Check(_main.Tools.PreviewEnabled != prev && prev, "Flow preview is not on by default, or its tab did not toggle it");
        await Click(C("FlowPreview"));
        Check(_main.Tools.PreviewEnabled, "Flow preview tab did not turn it back on");
        await Click(C("WearOverlay"));
        Check(_main.Ground.WearOverlay && _main.Ui.Overlay, "Wear overlay tab did not show the overlay");
        await Click(C("WearOverlay"));
        Check(!_main.Ground.WearOverlay, "Wear overlay tab did not hide it");
        Check(_main.Audio.Enabled, "sound is not on by default");
        await Click(C("Sound"));
        Check(!_main.Audio.Enabled && !((Button)C("Sound")).ButtonPressed, "Sound tab did not turn sound off");
        await Click(C("Sound"));
        Check(_main.Audio.Enabled, "Sound tab did not turn sound on");
        // Sliders: a click near the right end of the walker slider sets a high count; the volume slider follows a click too.
        var ws = (HSlider)C("Walkers");
        var r = ws.GetGlobalRect();
        await ClickAt(new Vector2(r.Position.X + r.Size.X * 0.95f, r.GetCenter().Y));
        Check(sim.WalkerCount >= 440, $"walker slider click set {sim.WalkerCount}");
        var vs = (HSlider)C("Volume");
        r = vs.GetGlobalRect();
        await ClickAt(new Vector2(r.Position.X + r.Size.X * 0.2f, r.GetCenter().Y));
        Check(_main.Audio.Volume < 0.4f, $"volume slider click set {_main.Audio.Volume}");
        // Reset starts the same seed over; New map moves to the next seed.
        sim.StepTicks(50);
        int seed = sim.Seed;
        await Click(C("Reset"));
        Check(sim.World.Ticks < 50 && sim.Seed == seed, "Reset tab did not start over");
        await Click(C("NewMap"));
        Check(sim.Seed == SimDriver.NextSeed(seed), "New map tab did not take the next seed");
        _notes.Add($"clicks=ok walkers={sim.WalkerCount}");
    }

    private async Task ClickAt(Vector2 p)
    {
        TestInput.Motion(p);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
    }

    private async Task Key(Key k) { TestInput.Key(k); await TestInput.Frames(this, 2); }

    private async Task Keys()
    {
        var sim = _main.Sim;
        // Move the pointer off the interface so nothing is hovered.
        TestInput.Motion(_main.Rig.LocalToScreen(new Vector3(128, 0, 128)));
        bool p = sim.Paused;
        await Key(Godot.Key.Space);
        Check(sim.Paused != p, "Space did not pause");
        await Key(Godot.Key.Space);
        Check(sim.Paused == p, "Space did not resume");
        Godot.Key[] ks = { Godot.Key.Key1, Godot.Key.Key2, Godot.Key.Key3, Godot.Key.Key4 };
        int[] sp = { 1, 2, 4, 8 };
        for (int i = 3; i >= 0; i--) { await Key(ks[i]); Check(sim.Speed == sp[i], $"key {i + 1} set speed {sim.Speed}"); }
        await Key(Godot.Key.F); Check(_main.Tools.Tool == Tool.Fence, "F");
        await Key(Godot.Key.B); Check(_main.Tools.Tool == Tool.Bridge, "B");
        await Key(Godot.Key.E); Check(_main.Tools.Tool == Tool.Erase, "E");
        await Key(Godot.Key.E); Check(_main.Tools.Tool == Tool.None, "E again should put the eraser down");
        await Key(Godot.Key.F); await Key(Godot.Key.Escape); Check(_main.Tools.Tool == Tool.None, "Escape");
        await Key(Godot.Key.P); Check(!_main.Tools.PreviewEnabled, "P off");
        await Key(Godot.Key.P); Check(_main.Tools.PreviewEnabled, "P on");
        await Key(Godot.Key.W); Check(_main.Ground.WearOverlay, "W on");
        Check(_main.Ui.Chapter.GetNode<Control>("Column/HeatLegend").Visible || _main.Ui.Chapter.Slip, "heat legend not shown with the overlay");
        await Key(Godot.Key.W); Check(!_main.Ground.WearOverlay, "W off");
        await Key(Godot.Key.M); Check(!_main.Audio.Enabled, "M off");
        await Key(Godot.Key.M); Check(_main.Audio.Enabled, "M on");
        sim.StepTicks(30);
        await Key(Godot.Key.R); Check(sim.World.Ticks < 30, "R did not reset");
        int seed = sim.Seed;
        await Key(Godot.Key.N); Check(sim.Seed == SimDriver.NextSeed(seed), "N did not make a new map");
        bool book = _main.Rig.InPlayView;
        await Key(Godot.Key.Home); Check(_main.Rig.InPlayView != book, "Home did not switch views");
        await Key(Godot.Key.Home);
        _notes.Add("keys=ok");
    }

    private async Task Focus()
    {
        await Key(Godot.Key.Tab);
        var f = GetViewport().GuiGetFocusOwner();
        Check(f != null, "Tab gave no control the keyboard focus");
        var box = f!.GetThemeStylebox("focus") as StyleBoxFlat;
        Check(box != null && box.BorderWidthTop >= 2, "focused control has no visible focus frame");
        // Space still pauses while a tab has focus; Enter presses the focused tab.
        bool p = _main.Sim.Paused;
        await Key(Godot.Key.Space);
        Check(_main.Sim.Paused != p, "Space did not pause while a control had focus");
        await Key(Godot.Key.Space);
        ((Control)C("WearOverlay")).GrabFocus();
        await Key(Godot.Key.Enter);
        Check(_main.Ground.WearOverlay, "Enter did not press the focused tab");
        await Key(Godot.Key.Enter);
        await Key(Godot.Key.Escape);
        Check(GetViewport().GuiGetFocusOwner() == null, "Escape did not release focus");
        _notes.Add($"focus={f.Name}");
    }

    private async Task Page()
    {
        _main.Sim.StepTicks(3 * _main.Params.TicksPerDay);
        await TestInput.Frames(this, 40); // the page refreshes twice a second
        var ch = _main.Ui.Chapter;
        var day = ch.GetNode<Label>("Column/Date/Value").Text;
        Check(day.Contains("ay ") && day.Contains("year"), $"date entry reads '{day}'");
        Check(ch.GetNode<Label>("Column/Walkers/Value").Text.Contains("out of"), "walkers entry empty");
        Check(ch.GetNode<Label>("Column/Map/Value").Text.StartsWith("Seed "), "seed entry empty");
        foreach (var n in new[] { "Detourratio", "Trailcoverage", "Trunkshare" })
            Check(ch.GetNode<Label>($"Column/{n}/Value").Text.Length > 0, $"{n} empty");
        Check(!ch.Slip, "chapter page folded into a slip in the play view");
        int smallest = SmallestFont(_main.Ui);
        Check(smallest >= 12, $"interface text as small as {smallest} px at 1280 x 720");
        _notes.Add($"page='{day}' smallest={smallest}px");
        await LabelsInside("1280x720");
    }

    /// <summary>
    /// In the whole-book view and in the play view: every visible label lies inside its page (at least
    /// ChapterPage.MinInset px from the page's edges, or inside the slip when the page is folded) or inside the panel
    /// or tab that holds it, and on screen.
    /// </summary>
    private async Task LabelsInside(string size)
    {
        foreach (bool book in new[] { true, false })
        {
            if (book) _main.Rig.FrameBook(instant: true); else _main.Rig.FramePlay(instant: true);
            await TestInput.Frames(this, 40); // the page refits a step a frame; the fps plate shows after half a second
            var vp = GetViewport().GetVisibleRect();
            var ch = _main.Ui.Chapter;
            var page = ch.Slip ? ch.GetGlobalRect() : ch.PageRect.Grow(-ChapterPage.MinInset);
            int n = 0;
            foreach (var l in Labels(_main.Ui))
            {
                var r = l.GetGlobalRect();
                Control? holder = null;
                for (var a = l.GetParent(); a != null && holder == null; a = a.GetParent())
                    if (a == ch) break;
                    else if (a is Button or PanelContainer && a is Control ac) holder = ac;
                bool onPage = holder == null && IsUnder(l, ch);
                var room = onPage ? page : holder?.GetGlobalRect() ?? vp;
                string where = onPage ? (ch.Slip ? "the chapter slip" : "the chapter page") : holder?.Name ?? "the window";
                Check(room.Grow(0.5f).Encloses(r), $"{size} {(book ? "book" : "play")} view: label {l.GetPath()} '{Short(l.Text)}' at {r} is not inside {where} {room}");
                Check(vp.Grow(0.5f).Encloses(r), $"{size} {(book ? "book" : "play")} view: label {l.GetPath()} '{Short(l.Text)}' at {r} is off screen");
                n++;
            }
            _notes.Add($"{size}-{(book ? "book" : "play")}: {n} labels inside (folded {ch.Folded})");
        }
    }

    private static bool IsUnder(Node n, Node ancestor)
    {
        for (var a = n.GetParent(); a != null; a = a.GetParent()) if (a == ancestor) return true;
        return false;
    }

    private static string Short(string t) => t.Length > 24 ? t.Substring(0, 24) + "..." : t;

    private static List<Label> Labels(Node n, List<Label>? into = null)
    {
        into ??= new List<Label>();
        if (n is Label l && l.IsVisibleInTree() && l.Text.Length > 0) into.Add(l);
        foreach (var c in n.GetChildren()) Labels(c, into);
        return into;
    }

    private static int SmallestFont(Node n)
    {
        int best = 999;
        if (n is Label l && l.IsVisibleInTree() && l.Text.Length > 0) best = l.GetThemeFontSize("font_size");
        foreach (var c in n.GetChildren()) best = Math.Min(best, SmallestFont(c));
        return best;
    }

    private async Task Resize()
    {
        int rebuilds = _main.Ui.Rebuilds;
        GetWindow().Size = new Vector2I(1920, 1080);
        await TestInput.Frames(this, 6);
        Check(_main.Ui.Rebuilds > rebuilds, "the interface was not rebuilt for the new window size");
        Check(Mathf.IsEqualApprox(_main.Ui.UiScale, 1f), $"interface scale {_main.Ui.UiScale} at 1080p");
        Controls();
        int smallest = SmallestFont(_main.Ui);
        Check(smallest >= 12, $"interface text as small as {smallest} px at 1920 x 1080");
        int body = _main.Ui.Chapter.GetNode<Label>("Column/Date/Value").GetThemeFontSize("font_size");
        Check(body >= 16, $"body text is {body} px at 1080p (UX tab: 16 px)");
        _notes.Add($"1080p smallest={smallest}px");
        await LabelsInside("1920x1080");
    }

    private static void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }
}
