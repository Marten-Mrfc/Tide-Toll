using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Renders the main scene in a real window (run under xvfb on Linux), saves a PNG to the
/// path given as --out=..., and fails if the image is blank. Must not run with --headless:
/// the dummy renderer draws nothing.
/// </summary>
public partial class Screenshot : Node
{
    private const int WaitFrames = 30;
    private int _frame;
    private string _out = "user://main.png";

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--out=")) _out = a.Substring(6);
        AddChild(GD.Load<PackedScene>("res://main.tscn").Instantiate());
    }

    public override void _Process(double delta)
    {
        if (++_frame < WaitFrames) return;
        try
        {
            if (DisplayServer.GetName() == "headless") { Quit(1, "screenshots need a window, not --headless"); return; }
            var img = GetViewport().GetTexture().GetImage();
            var err = img.SavePng(_out);
            if (err != Error.Ok) { Quit(1, $"could not save {_out}: {err}"); return; }
            // A blank or single-colour frame means nothing rendered.
            var colours = new HashSet<Color>();
            for (int y = 0; y < img.GetHeight(); y += 16)
                for (int x = 0; x < img.GetWidth(); x += 16)
                    colours.Add(img.GetPixel(x, y));
            if (colours.Count < 8) { Quit(1, $"image looks blank ({colours.Count} colours)"); return; }
            GD.Print($"SCREENSHOT PASS {_out} {img.GetWidth()}x{img.GetHeight()} colours={colours.Count} renderer={RenderingServer.GetCurrentRenderingMethod()}/{RenderingServer.GetCurrentRenderingDriverName()}");
            GetTree().Quit(0);
        }
        catch (Exception e) { Quit(1, e.ToString()); }
    }

    private void Quit(int code, string why)
    {
        GD.PrintErr("SCREENSHOT FAIL: " + why);
        GetTree().Quit(code);
    }
}
