using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the settings pages, driven with real mouse and key events (Input.ParseInputEvent): every slider
/// moves its bus; every Display choice reaches the display seam, with the "Keep this?" slip and its 10-second
/// fallback; the interface size sets the window's content scale; a key is rebound, a clash is shown and resolved or
/// cancelled, Esc cancels a rebind, the new key fires its action, defaults come back; every Saving control calls the
/// fake save system; closing and reopening keeps everything; a damaged file loads as defaults; the whole screen is
/// driven with the keyboard alone with a visible focus; and the spread fits at 1280 x 720 and 1920 x 1080 at 80 and
/// 150 percent. Prints SETTINGS PASS or SETTINGS FAIL and quits with 0 or 1.
/// </summary>
public partial class SettingsTest : Node
{
    private const string FilePath = "user://settings-test.cfg";
    private SettingsRuntime _rt = null!;
    private RecordingDisplay _display = null!;
    private FakeSaves _saves = null!;
    private SettingsPages _pages = null!;
    private int _closed;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        Run();
    }

    private async void Run()
    {
        try
        {
            if (FileAccess.FileExists(FilePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FilePath));
            Open(new RecordingDisplay(), new FakeSaves());
            await Frames(4);
            Check(_rt.Store.LoadedFromFile == false, "a missing file should load as defaults");
            Check(InputMap.HasAction("tt_buy_5") && InputMap.HasAction("tt_achievements"), "actions not registered");
            await Sound();
            await Display();
            await Keys();
            await Saving();
            await Reopen();
            Damaged();
            await KeyboardOnly();
            await Fits();
            GD.Print("SETTINGS PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("SETTINGS FAIL: " + e.Message);
            GetTree().Quit(1);
        }
    }

    // ---------------------------------------------------------------- set-up and helpers

    private void Open(RecordingDisplay display, FakeSaves saves)
    {
        _display = display;
        _saves = saves;
        _rt = new SettingsRuntime { SettingsPath = FilePath, Display = display };
        AddChild(_rt);
        OpenPages();
    }

    private void OpenPages()
    {
        _pages = new SettingsPages(_rt, _saves);
        _pages.Closed += () => _closed++;
        AddChild(_pages);
    }

    private async Task CloseAll()
    {
        _pages.QueueFree();
        _rt.QueueFree();
        await Frames(2);
    }

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception(what);
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private Control C(string name)
    {
        Check(_pages.Controls.TryGetValue(name, out var c), $"control {name} missing in section {_pages.Section}");
        return c!;
    }

    /// <summary>A point of a control in window pixels (through the content scale).</summary>
    private Vector2 At(Control c, float fx = 0.5f, float fy = 0.5f)
    {
        var r = c.GetGlobalRect();
        return GetViewport().GetFinalTransform() * (r.Position + r.Size * new Vector2(fx, fy));
    }

    private async Task Click(Control c, float fx = 0.5f)
    {
        var p = At(c, fx);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames(3);
    }

    private async Task Click(string name, float fx = 0.5f) => await Click(C(name), fx);

    private async Task Key(Key key, bool shift = false, bool release = true)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = shift });
        await Frames(1);
        if (release) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, ShiftPressed = shift });
        await Frames(2);
    }

    private static void Near(float a, float b, string what) => Check(Mathf.Abs(a - b) < 0.01f, $"{what}: {a} is not {b}");

    // ---------------------------------------------------------------- Sound

    private async Task Sound()
    {
        Check(_pages.Section == "Sound", "the pages should open on Sound");
        foreach (var bus in GameSettings.Buses)
        {
            var slider = (HSlider)C("sound." + bus);
            int idx = AudioServer.GetBusIndex(bus);
            Check(idx >= 0, $"bus {bus} was not created");
            float before = AudioServer.GetBusVolumeDb(idx);
            await Click(slider, 0.3f);
            int v = _rt.Store.Volume(bus);
            Check(v > 10 && v < 50, $"{bus}: a click at 30 percent of the slider gave {v}");
            Near(AudioServer.GetBusVolumeDb(idx), SettingsApplier.VolumeDb(v), $"{bus} bus volume");
            Check(AudioServer.GetBusVolumeDb(idx) < before - 1, $"{bus} bus volume did not go down");
            Check(!AudioServer.IsBusMute(idx), $"{bus} muted at {v}");
        }
        // The far left end is silence.
        await Click("sound.Effects", 0.002f);
        int fx = AudioServer.GetBusIndex("Effects");
        Check(_rt.Store.Volume("Effects") == 0 && AudioServer.IsBusMute(fx), "Effects at the left end should be 0 and muted");
        await Click("sound.Effects", 0.998f);
        Check(_rt.Store.Volume("Effects") == 100 && !AudioServer.IsBusMute(fx), $"Effects at the right end should be 100, got {_rt.Store.Volume("Effects")}");
        Near(AudioServer.GetBusVolumeDb(fx), 0f, "Effects at 100");

        await Click("sound.mute");
        Check(_rt.Store.MuteWhenInactive, "mute switch did not turn on");
        int master = AudioServer.GetBusIndex("Master");
        _rt.Notification((int)NotificationApplicationFocusOut);
        Check(AudioServer.IsBusMute(master), "Master not muted when the window lost focus");
        _rt.Notification((int)NotificationApplicationFocusIn);
        Check(!AudioServer.IsBusMute(master), "Master still muted when the window came back");
        _notes.Add("sound");
    }

    // ---------------------------------------------------------------- Display

    private async Task Display()
    {
        await Click("ribbon.Display");
        Check(_pages.Section == "Display", "Display ribbon did not open Display");

        // Window mode: forward to Borderless, Keep.
        await Click("display.mode", 0.9f);
        Check(_display.Mode == WindowMode.Borderless, "the seam did not get Borderless");
        Check(_pages.Slip == "keep", "no Keep this? after a window mode change");
        await Click("slip.0");
        Check(_rt.Store.WindowMode == WindowMode.Borderless && _pages.Slip == "", "Keep did not keep Borderless");
        // Full screen, then Go back.
        await Click("display.mode", 0.9f);
        Check(_display.Mode == WindowMode.Fullscreen, "the seam did not get Full screen");
        await Click("slip.1");
        Check(_display.Mode == WindowMode.Borderless && _rt.Store.WindowMode == WindowMode.Borderless, "Go back did not restore Borderless");
        Check(((ChoiceButton)C("display.resolution")).Disabled, "resolution should be off outside windowed mode");
        // Back to Windowed, kept.
        await Click("display.mode", 0.1f);
        Check(_display.Mode == WindowMode.Windowed, "the seam did not get Windowed");
        await Click("slip.0");
        Check(_rt.Store.WindowMode == WindowMode.Windowed, "Windowed not kept");

        // Resolution: the list runs from 1280 x 720 to the (fake) screen's 2560 x 1440.
        var res = (ChoiceButton)C("display.resolution");
        Check(!res.Disabled && res.Options[0] == "1280 x 720" && res.Options[^1] == "2560 x 1440", "resolution list: " + string.Join(", ", res.Options));
        await Click(res, 0.9f);
        Check(_display.Size == new Vector2I(1366, 768) && _pages.Slip == "keep", "the seam did not get 1366 x 768 on trial");
        // No answer: it waits, then goes back by itself after 10 seconds.
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < 8000) await Frames(1);
        Check(_pages.Slip == "keep" && _display.Size == new Vector2I(1366, 768), "the trial went back before 10 seconds");
        while (_pages.Slip == "keep" && Time.GetTicksMsec() - start < 13000) await Frames(1);
        float waited = (Time.GetTicksMsec() - start) / 1000f;
        Check(_pages.Slip == "" && _display.Size == new Vector2I(1280, 720), "the trial did not go back by itself");
        Check(waited > 9.5f && waited < 11f, $"went back after {waited:F1} s, not 10");
        Check(_rt.Store.Resolution == new Vector2I(1280, 720), "the store kept the size nobody confirmed");
        // And kept when confirmed.
        await Click("display.resolution", 0.9f);
        await Click("slip.0");
        Check(_rt.Store.Resolution == new Vector2I(1366, 768) && _display.Size == new Vector2I(1366, 768), "1366 x 768 not kept");

        await Click("display.vsync");
        Check(!_rt.Store.Vsync && !_display.Vsync, "VSync off did not reach the seam");
        // Frame limit: None, then left through 120, 60, 30.
        foreach (int fps in new[] { 120, 60, 30 })
        {
            await Click("display.fps", 0.1f);
            Check(_display.Fps == fps && _rt.Store.FrameLimit == fps, $"frame limit {fps} did not reach the seam (got {_display.Fps})");
        }
        await Click("display.fps", 0.9f);
        Check(_display.Fps == 60, "frame limit back to 60");

        // Interface size, applied through the window's content scale.
        await Click("display.interface", 0.9f);
        await Frames(3);
        Check(_rt.Store.InterfaceSize == 110, "interface size did not go to 110");
        Near(GetWindow().ContentScaleFactor, 1.1f, "content scale at 110 percent");
        // Clicks still land where they should at the new scale.
        await Click("display.interface", 0.1f);
        await Frames(3);
        Near(GetWindow().ContentScaleFactor, 1.0f, "content scale back at 100 percent");
        await Click("display.interface", 0.9f);
        await Frames(3);
        Near(GetWindow().ContentScaleFactor, 1.1f, "content scale at 110 percent again");

        await Click("display.coins");
        Check(!_rt.Store.ShowCoinNumbers, "coin numbers did not turn off");
        await Click("display.follow");
        Check(!_rt.Store.FollowTown, "follow the town did not turn off");
        _notes.Add($"display({_display.Calls.Count} seam calls)");
    }
}
