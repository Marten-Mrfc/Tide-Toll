using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// Screenshot of one settings section. Arguments after "--": --out-dir=DIR --section=Sound|Display|Keys|Saving
/// --ui=PERCENT --name=FILE (without .png) --slip=keep|clash (Display or Keys with that slip open). Uses its own settings file; the window is whatever --resolution gave.
/// Prints SETTINGS SHOT PASS or quits with 1.
/// </summary>
public partial class SettingsShot : Node
{
    public override void _Ready() => Run();

    private async void Run()
    {
        try
        {
            string dir = "user://", section = "Sound", name = "settings", slip = "";
            int ui = 100;
            foreach (var a in OS.GetCmdlineUserArgs())
            {
                if (a.StartsWith("--out-dir=")) dir = a[10..].TrimEnd('/') + "/";
                else if (a.StartsWith("--section=")) section = a[10..];
                else if (a.StartsWith("--ui=")) ui = int.Parse(a[5..]);
                else if (a.StartsWith("--name=")) name = a[7..];
                else if (a.StartsWith("--slip=")) slip = a[7..];
            }
            const string path = "user://settings-shot.cfg";
            if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            var rt = new SettingsRuntime { SettingsPath = path, Display = new RecordingDisplay() };
            AddChild(rt);
            rt.Store.SetInterfaceSize(ui);
            var pages = new SettingsPages(rt, new FakeSaves());
            AddChild(pages);
            await Frames(3);
            pages.ShowSection(section);
            await Frames(4);
            if (section == "Keys") { pages.FocusName("keys.tt_bell.0"); }
            if (slip == "keep") ((ChoiceButton)pages.Controls["display.mode"]).SetIndex(1, true);
            if (slip == "clash") { pages.StartCapture("tt_pave", 0); await Frames(2); TestKey(Key.Space); }
            await Frames(6);
            DirAccess.MakeDirRecursiveAbsolute(dir);
            var img = GetViewport().GetTexture().GetImage();
            var file = dir + name + ".png";
            if (img.SavePng(file) != Error.Ok) throw new Exception("could not save " + file);
            GD.Print($"SETTINGS SHOT PASS {file} {img.GetWidth()}x{img.GetHeight()}");
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("SETTINGS SHOT FAIL: " + e.Message);
            GetTree().Quit(1);
        }
    }

    private static void TestKey(Key k)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
