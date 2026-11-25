using System;
using System.Collections.Generic;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>A save system for the settings tests: counts every call and keeps a pretend save.</summary>
public sealed class FakeSaves : ISaveActions
{
    public int Saved, Loaded, Deleted;
    public bool HasSave { get; set; } = true;
    public DateTime? LastSaveTime { get; set; } = new DateTime(2026, 11, 25, 20, 31, 0);
    public string FolderPath => "/home/player/.local/share/godot/app_userdata/Tide & Toll/saves";
    public bool SaveNow() { Saved++; HasSave = true; LastSaveTime = new DateTime(2026, 11, 25, 21, 5, 0); return true; }
    public bool LoadLast() { Loaded++; return HasSave; }
    public void DeleteSave() { Deleted++; HasSave = false; LastSaveTime = null; }
}

/// <summary>
/// A display seam that records what would be applied to the window instead of changing it (no real monitor in a
/// headless test). The content scale is applied for real, so the test can see the interface size take effect.
/// </summary>
public sealed class RecordingDisplay : DisplaySeam
{
    public readonly List<string> Calls = new();
    public WindowMode Mode = WindowMode.Windowed;
    public Vector2I Size = new(1280, 720);
    public bool Vsync = true;
    public int Fps = -1;
    public override Vector2I ScreenSize() => new(2560, 1440);
    public override void ApplyWindow(Window w, WindowMode mode, Vector2I size) { Mode = mode; Size = size; Calls.Add($"window {mode} {size.X}x{size.Y}"); }
    public override void ApplyVsync(bool on) { Vsync = on; Calls.Add($"vsync {on}"); }
    public override void ApplyFrameLimit(int fps) { Fps = fps; Calls.Add($"fps {fps}"); }
}
