using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Renders the main scene in a real window (xvfb on Linux) and saves PNGs into the folder given as
/// --out-dir=...: book.png (the whole open book at the start), map.png (the map page in the play view after
/// about 20 in-game days at 8x, seed 1, 200 walkers, the toy's defaults) and close.png (a closer look at the
/// hamlet). The 20 days are fast-forwarded through the driver at 8x rather than waited for, because software
/// rendering here draws only a few frames a second. Fails if an image is blank or if the map view has no
/// trail-coloured pixels. Must not run with --headless: the dummy renderer draws nothing.
/// </summary>
public partial class Screenshot : Node
{
    private string _dir = "user://";
    private Main _main = null!;
    private bool _failed;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--out-dir=")) _dir = a.Substring(10).TrimEnd('/') + "/";
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") { Quit(1, "screenshots need a window, not --headless"); return; }
            _main.Rig.FrameBook(instant: true);
            await Frames(20);
            Check(await Snap("book"), "book");

            // About 20 in-game days at 8x, fast-forwarded, then a few real frames at 8x so walkers are mid-stride.
            _main.Sim.SetSpeed(8);
            _main.Sim.StepTicks(20 * _main.Params.TicksPerDay);
            _main.Ground.SnapNext();
            _main.Ground.Upload();
            _main.Rig.FramePlay(instant: true);
            await Frames(8);
            var map = await Snap("map");
            Check(map, "map");
            if (map != null)
            {
                int trail = TrailPixels(map);
                double share = trail * 100.0 / (map.GetWidth() * map.GetHeight() / 4);
                GD.Print($"SCREENSHOT map trail-coloured pixels: {trail} ({share:F2}% of sampled pixels), day {_main.Sim.World.Stats().Day}, " +
                         $"walkers out {_main.Sim.World.WalkersOut()}, draw calls {DrawCalls()}");
                if (share < 0.5) Quit(1, $"map view has too few trail-coloured pixels ({share:F2}%)");
            }

            // Closer: the hamlet at play zoom (about 90 m of map in view).
            var b = _main.Sim.World.Buildings[_main.Sim.World.Houses[0]];
            _main.Rig.LookAt(new Vector3(b.X * 2f + 10, 0, b.Y * 2f + 16), 90f);
            await Frames(6);
            Check(await Snap("close"), "close");
            if (!_failed)
            {
                GD.Print($"SCREENSHOT PASS {_dir}book.png map.png close.png {GetViewport().GetVisibleRect().Size} " +
                         $"renderer={RenderingServer.GetCurrentRenderingMethod()}/{RenderingServer.GetCurrentRenderingDriverName()}");
                GetTree().Quit(0);
            }
        }
        catch (Exception e) { Quit(1, e.ToString()); }
    }

    private static long DrawCalls() => (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);

    private async System.Threading.Tasks.Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private async System.Threading.Tasks.Task<Image?> Snap(string name)
    {
        await Frames(2);
        var img = GetViewport().GetTexture().GetImage();
        string path = _dir + name + ".png";
        if (img.SavePng(path) != Error.Ok) { Quit(1, "could not save " + path); return null; }
        // A blank or single-colour frame means nothing rendered.
        var colours = new HashSet<Color>();
        for (int y = 0; y < img.GetHeight(); y += 16)
            for (int x = 0; x < img.GetWidth(); x += 16)
                colours.Add(img.GetPixel(x, y));
        GD.Print($"SCREENSHOT {name}: {path} colours={colours.Count}");
        if (colours.Count < 40) { Quit(1, $"{name} looks blank ({colours.Count} colours)"); return null; }
        return img;
    }

    /// <summary>Pixels (every second row and column) close to the ground textures' trail and track browns.</summary>
    private static int TrailPixels(Image img)
    {
        Color[] browns = { Palette.TrailEarth, Palette.PackedEarth, Color.FromHtml("#A07A50") };
        int n = 0;
        for (int y = 0; y < img.GetHeight(); y += 2)
            for (int x = 0; x < img.GetWidth(); x += 2)
            {
                var c = img.GetPixel(x, y);
                foreach (var t in browns)
                {
                    float d = MathF.Abs(c.R - t.R) + MathF.Abs(c.G - t.G) + MathF.Abs(c.B - t.B);
                    // Brown: red above green above blue, near one of the palette browns.
                    if (d < 0.16f && c.R > c.G && c.G > c.B) { n++; break; }
                }
            }
        return n;
    }

    private void Check(object? ok, string what)
    {
        if (ok == null) _failed = true;
    }

    private void Quit(int code, string why)
    {
        _failed = true;
        GD.PrintErr("SCREENSHOT FAIL: " + why);
        GetTree().Quit(code);
    }
}
