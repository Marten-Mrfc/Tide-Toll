using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Renders the main scene in a real window (xvfb on Linux) and saves PNGs into the folder given as
/// --out-dir=...: book.png (the whole open book with the interface at the start), map.png (the map page in the play
/// view after about 20 in-game days at 8x, seed 1, 200 walkers, the toy's defaults), wear.png (the wear overlay),
/// preview.png (a fence being dragged across the busiest trail with the flow preview showing, driven by real mouse
/// events), each also saved as NAME-WxH.png for the window size, then close.png (a closer look at the hamlet),
/// field.png, market.png, fence.png and bridge.png (close-ups). With "-- --ui-only" only the four interface views
/// are taken (the 1920 x 1080 run). The 20 days are fast-forwarded through the driver at 8x rather than waited for, because software
/// rendering here draws only a few frames a second. Fails if an image is blank or if the map view has no
/// trail-coloured pixels. Must not run with --headless: the dummy renderer draws nothing.
/// </summary>
public partial class Screenshot : Node
{
    private string _dir = "user://";
    private bool _uiOnly;
    private string _suffix = "";
    private Main _main = null!;
    private bool _failed;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a.Substring(10).TrimEnd('/') + "/";
            if (a == "--ui-only") _uiOnly = true;
        }
        Input.UseAccumulatedInput = false;
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
            var vs = GetViewport().GetVisibleRect().Size;
            _suffix = $"-{vs.X}x{vs.Y}";
            await Frames(4);
            _main.Rig.FrameBook(instant: true);
            await Frames(20);
            Check(await Snap("book", true), "book");

            // About 20 in-game days at 8x, fast-forwarded, then a few real frames at 8x so walkers are mid-stride.
            _main.Sim.SetSpeed(8);
            _main.Sim.StepTicks(20 * _main.Params.TicksPerDay);
            _main.Ground.SnapNext();
            _main.Ground.Upload();
            _main.Rig.FramePlay(instant: true);
            await Frames(8);
            var map = await Snap("map", true);
            Check(map, "map");
            if (map != null)
            {
                int trail = TrailPixels(map);
                double share = trail * 100.0 / (map.GetWidth() * map.GetHeight() / 4);
                GD.Print($"SCREENSHOT map trail-coloured pixels: {trail} ({share:F2}% of sampled pixels), day {_main.Sim.World.Stats().Day}, " +
                         $"walkers out {_main.Sim.World.WalkersOut()}, draw calls {DrawCalls()}");
                if (share < 0.5) Quit(1, $"map view has too few trail-coloured pixels ({share:F2}%)");
            }
            _main.Ui.SetOverlay(true);
            await Frames(10);
            Check(await Snap("wear", true), "wear");
            _main.Ui.SetOverlay(false);
            await PreviewShot();
            if (_uiOnly) { Done("book map wear preview"); return; }

            // Closer: the hamlet at play zoom (about 90 m of map in view).
            var b = _main.Sim.World.Buildings[_main.Sim.World.Houses[0]];
            _main.Rig.LookAt(new Vector3(b.X * 2f + 10, 0, b.Y * 2f + 16), 90f);
            await Frames(6);
            Check(await Snap("close"), "close");
            // The field and the market up close (crop rows, stalls), for looking at.
            var w = _main.Sim.World;
            var f = w.Buildings[w.OfKind(TideToll.Sim.BuildingKind.Field)[0]];
            _main.Rig.LookAt(new Vector3((f.X + f.W / 2f) * 2f, 0, (f.Y + f.H / 2f) * 2f + 8f), 50f);
            await Frames(4);
            Check(await Snap("field"), "field");
            var m = w.Buildings[w.OfKind(TideToll.Sim.BuildingKind.Market)[0]];
            _main.Rig.LookAt(new Vector3((m.X + m.W / 2f) * 2f, 0, (m.Y + m.H / 2f) * 2f + 4f), 40f);
            await Frames(4);
            Check(await Snap("market"), "market");
            // Edits: a fence line across the ground below the hamlet and a footbridge, after two more days.
            int n = w.Grid.Width, by = Math.Clamp(w.Map.FordY - 24, 12, n - 12);
            var hb = w.Buildings[w.Houses[0]];
            int fy = Math.Min(n - 4, hb.Y + hb.H + 6);
            _main.Sim.PlaceFenceLine(hb.X - 6, fy, hb.X + 10, fy + 5);
            var bridge = _main.Sim.PlaceBridge((int)w.RiverX[by], by);
            _main.Sim.StepTicks(2 * _main.Params.TicksPerDay);
            _main.Ground.SnapNext();
            _main.Ground.Upload();
            _main.Rig.LookAt(new Vector3(hb.X * 2f + 4, 0, fy * 2f + 4), 70f);
            await Frames(4);
            Check(await Snap("fence"), "fence");
            if (bridge != null)
            {
                _main.Rig.LookAt(new Vector3(w.RiverX[by] * 2f, 0, by * 2f + 1), 40f);
                await Frames(4);
                Check(await Snap("bridge"), "bridge");
            }
            // Hovering the other two tools: a footbridge further up the river, then the eraser over the new fence.
            int hr = Math.Max(6, by - 14);
            await HoverShot(Godot.Key.B, Vector2.Zero, "bridge-hover", new Vector3(w.RiverX[hr] * 2f, 0, hr * 2f), 120f, (int)w.RiverX[hr], hr);
            await HoverShot(Godot.Key.E, Vector2.Zero, "erase-hover", new Vector3(hb.X * 2f + 4, 0, fy * 2f + 4), 90f, hb.X + 2, fy + 1);
            Done("book map wear preview close field market fence bridge bridge-hover erase-hover");
        }
        catch (Exception e) { Quit(1, e.ToString()); }
    }

    private void Done(string names)
    {
        if (_failed) return;
        GD.Print($"SCREENSHOT PASS {_dir} {names} (also *{_suffix}.png) {GetViewport().GetVisibleRect().Size} " +
                 $"renderer={RenderingServer.GetCurrentRenderingMethod()}/{RenderingServer.GetCurrentRenderingDriverName()}");
        GetTree().Quit(0);
    }

    /// <summary>
    /// A fence dragged across the busiest trail with real mouse events, held until the flow preview has settled, then
    /// photographed with the readout by the pointer; Escape puts the tool down without placing it.
    /// </summary>
    private async System.Threading.Tasks.Task PreviewShot()
    {
        var w = _main.Sim.World;
        int n = w.Grid.Width, best = 0;
        float bw = -1;
        for (int y = 16; y < n - 16; y++)
            for (int x = 16; x < n - 16; x++)
            {
                int c = y * n + x;
                bool clear = true;
                for (int k = -5; k <= 5 && clear; k++) clear = w.CanFence(c + k);
                if (clear && w.Wear[c] > bw) { bw = w.Wear[c]; best = c; }
            }
        int bx = best % n, by = best / n;
        _main.Sim.SetPaused(true);
        Key(Godot.Key.F);
        await Frames(2);
        var a = Cell(bx - 5, by);
        var b = Cell(bx + 5, by + 1);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        await Frames(1);
        for (int i = 1; i <= 4; i++)
        {
            var p = a.Lerp(b, i / 4f);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left });
            await Frames(1);
        }
        for (int i = 0; i < 60 && !_main.Tools.Previewer.Settled; i++) await Frames(1);
        await Frames(6); // the routes fade in
        GD.Print($"SCREENSHOT preview: fence {bx - 5},{by} to {bx + 5},{by + 1}, settled={_main.Tools.Previewer.Settled}, '{_main.Tools.Previewer.Text}', routes {_main.Tools.Routes.Routes}");
        if (!_main.Tools.Previewer.Settled || _main.Tools.Routes.Routes == 0) { Quit(1, "the flow preview did not show during the fence drag"); return; }
        Check(await Snap("preview", true), "preview");
        Key(Godot.Key.Escape);
        await Frames(2);
        _main.Sim.SetPaused(false);
    }

    private async System.Threading.Tasks.Task HoverShot(Key tool, Vector2 at, string name, Vector3 look, float size, int cx = -1, int cy = -1)
    {
        _main.Sim.SetPaused(true);
        _main.Rig.LookAt(look, size);
        await Frames(3);
        if (cx >= 0) at = Cell(cx, cy);
        Key(tool);
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Frames(2);
        for (int i = 0; i < 60 && !_main.Tools.Previewer.Settled; i++) await Frames(1);
        await Frames(6);
        GD.Print($"SCREENSHOT {name}: '{_main.Tools.LastMessage}' preview '{_main.Tools.Previewer.Text}' ghost {_main.Tools.Ghost.Pieces}");
        Check(await Snap(name), name);
        Key(Godot.Key.Escape);
        await Frames(1);
        _main.Sim.SetPaused(false);
    }

    private Vector2 Cell(int x, int y) => _main.Rig.LocalToScreen(new Vector3((x + 0.5f) * 2f, 0, (y + 0.5f) * 2f));

    private static void Key(Key k)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
    }

    private static long DrawCalls() => (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);

    private async System.Threading.Tasks.Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private async System.Threading.Tasks.Task<Image?> Snap(string name, bool sized = false)
    {
        await Frames(2);
        var img = GetViewport().GetTexture().GetImage();
        string path = _dir + name + ".png";
        if (!_uiOnly && img.SavePng(path) != Error.Ok) { Quit(1, "could not save " + path); return null; }
        if (sized && img.SavePng(_dir + name + _suffix + ".png") != Error.Ok) { Quit(1, "could not save " + name + _suffix); return null; }
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
