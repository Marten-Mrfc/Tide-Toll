using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// Every call that changes the real window or the frame rate goes through here, so a test can subclass it and check
/// what would be applied without a real monitor. The default implementation talks to Godot's DisplayServer.
/// </summary>
public class DisplaySeam
{
    public static readonly Vector2I[] CommonSizes =
    {
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3200, 1800), new(3840, 2160),
    };

    /// <summary>The size of the screen the window is on (1920 x 1080 when the platform cannot say, e.g. headless).</summary>
    public virtual Vector2I ScreenSize()
    {
        var s = DisplayServer.ScreenGetSize();
        return s.X >= 640 && s.Y >= 360 ? s : new Vector2I(1920, 1080);
    }

    /// <summary>Window sizes to offer: 1280 x 720 up to the screen's size, plus the stored one if it is not a common size.</summary>
    public List<Vector2I> Resolutions(Vector2I current)
    {
        var screen = ScreenSize();
        var list = new List<Vector2I>();
        foreach (var r in CommonSizes)
            if (r.X <= screen.X && r.Y <= screen.Y) list.Add(r);
        if (list.Count == 0) list.Add(CommonSizes[0]);
        if (!list.Contains(current) && current.X <= screen.X && current.Y <= screen.Y) list.Add(current);
        list.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return list;
    }

    /// <summary>Window mode, and the window's size when windowed (centred on its screen).</summary>
    public virtual void ApplyWindow(Window w, WindowMode mode, Vector2I size)
    {
        switch (mode)
        {
            case WindowMode.Windowed:
                w.Mode = Window.ModeEnum.Windowed;
                w.Size = size;
                var screen = ScreenSize();
                w.Position = DisplayServer.ScreenGetPosition(w.CurrentScreen) + (screen - size) / 2;
                break;
            case WindowMode.Borderless: w.Mode = Window.ModeEnum.Fullscreen; break;          // borderless, desktop size
            case WindowMode.Fullscreen: w.Mode = Window.ModeEnum.ExclusiveFullscreen; break;
        }
    }

    public virtual void ApplyVsync(bool on)
        => DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

    /// <summary>0 means no limit.</summary>
    public virtual void ApplyFrameLimit(int fps) => Engine.MaxFps = fps;

    /// <summary>Interface size: the window's content scale (1.0 = 100 percent).</summary>
    public virtual void ApplyContentScale(Window w, float scale) => w.ContentScaleFactor = scale;
}
