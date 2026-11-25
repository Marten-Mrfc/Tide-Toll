using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>The keyboard alone, with a visible focus at every step; and the fit at two window sizes and two interface sizes.</summary>
public partial class SettingsTest
{
    private int _keySteps;

    /// <summary>The focused control exists, is shown and draws a visible focus frame.</summary>
    private void FocusVisible()
    {
        var f = GetViewport().GuiGetFocusOwner();
        Check(f != null && f.IsVisibleInTree(), $"no visible keyboard focus in {_pages.Section}");
        var box = f!.GetThemeStylebox("focus") as StyleBoxFlat;
        Check(box != null && box.BorderWidthTop >= 2 && box.BorderColor.A > 0.9f, $"focus on {f.Name} has no visible frame");
        Check(GetViewport().GetVisibleRect().Encloses(f.GetGlobalRect()), $"focused {f.Name} is off screen: {f.GetGlobalRect()}");
    }

    private async Task Press(Key key, bool shift = false)
    {
        await Key(key, shift);
        _keySteps++;
        FocusVisible();
    }

    /// <summary>Tab (or Shift+Tab) until the named control has the focus.</summary>
    private async Task TabTo(string name, bool back = false)
    {
        for (int i = 0; i < 60; i++)
        {
            if (_pages.FocusedName() == name) return;
            await Press(Godot.Key.Tab, back);
        }
        throw new Exception($"Tab never reached {name} (focus on {_pages.FocusedName()})");
    }

    private async Task KeyboardOnly()
    {
        int closedBefore = _closed;
        FocusVisible();
        Check(_pages.FocusedName() == "ribbon.Sound", "the pages should open with the focus on the Sound ribbon");
        // Sound: Left on a slider.
        await TabTo("sound.Music");
        int music = _rt.Store.Volume("Music");
        for (int i = 0; i < 3; i++) await Press(Godot.Key.Left);
        Check(_rt.Store.Volume("Music") == music - 3, "Left on the Music slider");
        Near(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Music")), SettingsApplier.VolumeDb(music - 3), "Music bus by keyboard");
        await TabTo("sound.mute");
        await Press(Godot.Key.Left);
        Check(!_rt.Store.MuteWhenInactive, "Left on the mute switch should turn it off");

        // Arrows between ribbons; Enter opens a section.
        await TabTo("ribbon.Sound", true);
        await Press(Godot.Key.Down);
        Check(_pages.FocusedName() == "ribbon.Display", $"Down from the Sound ribbon went to {_pages.FocusedName()}");
        await Press(Godot.Key.Enter);
        Check(_pages.Section == "Display", "Enter on the Display ribbon");
        await TabTo("display.vsync");
        await Press(Godot.Key.Space);
        Check(_rt.Store.Vsync, "Space on VSync");
        await TabTo("display.interface");
        await Press(Godot.Key.Right);
        await Frames(3);
        Check(_rt.Store.InterfaceSize == 120, "Right on interface size");
        Near(GetWindow().ContentScaleFactor, 1.2f, "content scale by keyboard");
        Check(_pages.FocusedName() == "display.interface", "focus lost when the pages were rebuilt at the new size");
        await Press(Godot.Key.Left);
        await Frames(3);
        Check(_rt.Store.InterfaceSize == 110, "Left on interface size");
        // A window change: the slip holds the focus; Tab stays inside it; Enter keeps.
        await TabTo("display.mode");
        await Press(Godot.Key.Right);
        Check(_pages.Slip == "keep" && _pages.FocusedName() == "slip.0", "the Keep this? slip should take the focus");
        await Press(Godot.Key.Tab);
        Check(_pages.FocusedName() == "slip.1", "Tab in the slip");
        await Press(Godot.Key.Tab);
        Check(_pages.FocusedName() == "slip.0", "Tab should stay inside the slip");
        await Press(Godot.Key.Enter);
        Check(_rt.Store.WindowMode == WindowMode.Borderless, "Enter on Keep");
        await Press(Godot.Key.Left);
        Check(_pages.Slip == "keep", "second trial");
        await Press(Godot.Key.Escape);
        Check(_pages.Slip == "" && _rt.Store.WindowMode == WindowMode.Borderless && _display.Mode == WindowMode.Borderless, "Esc on the slip goes back");
        Check(_closed == closedBefore, "Esc on the slip closed the pages");

        // Keys: rebind with the keyboard, then restore.
        await TabTo("ribbon.Keys", true);
        await Press(Godot.Key.Enter);
        Check(_pages.Section == "Keys", "Enter on the Keys ribbon");
        await TabTo("keys.tt_pave.0");
        await Press(Godot.Key.Enter);
        Check(_pages.Capturing != null, "Enter on a key should wait for a new one");
        await Key(Godot.Key.L);
        Check(_rt.Store.KeyFor("tt_pave", 0) == Godot.Key.L, "L for Pave by keyboard");
        FocusVisible();
        await TabTo("keys.restore");
        await Press(Godot.Key.Space);
        Check(_rt.Store.KeyFor("tt_pave", 0) == Godot.Key.P, "Restore defaults by keyboard");

        // Saving.
        await TabTo("ribbon.Saving", true);
        await Press(Godot.Key.Enter);
        await TabTo("saving.save");
        int saved = _saves.Saved;
        await Press(Godot.Key.Enter);
        Check(_saves.Saved == saved + 1, "Save now by keyboard");
        await TabTo("saving.autosave");
        await Press(Godot.Key.Left);
        Check(_rt.Store.AutosaveMinutes == 2, "Left on autosave");

        // Esc goes back.
        await Key(Godot.Key.Escape);
        Check(_closed == closedBefore + 1, "Esc did not close the pages");
        _notes.Add($"keyboard({_keySteps} keys)");
    }

    private async Task Fits()
    {
        var sizes = new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) };
        var uis = new[] { 80, 100, 150 };
        foreach (var size in sizes)
            foreach (int ui in uis)
            {
                GetWindow().Size = size;
                _rt.Store.SetInterfaceSize(ui);
                await Frames(4);
                foreach (var section in SettingsPages.Sections)
                {
                    _pages.ShowSection(section);
                    await Frames(3);
                    var vp = GetViewport().GetVisibleRect();
                    string where = $"{size.X}x{size.Y} at {ui} percent, {section}";
                    foreach (var r in SettingsPages.Sections)
                        Check(vp.Encloses(C("ribbon." + r).GetGlobalRect()), $"ribbon {r} off screen at {where}");
                    Check(vp.Encloses(C("back").GetGlobalRect()), $"Back off screen at {where}");
                    foreach (var kv in _pages.Controls)
                    {
                        if (!kv.Key.Contains('.') || kv.Key.StartsWith("ribbon.") || kv.Key.StartsWith("slip.")) continue;
                        var scroll = kv.Value.GetParent();
                        while (scroll != null && scroll is not ScrollContainer) scroll = scroll.GetParent();
                        Check(scroll != null, $"{kv.Key} is not on a page");
                        var page = ((Control)scroll!).GetGlobalRect();
                        var rect = kv.Value.GetGlobalRect();
                        Check(rect.Position.X >= page.Position.X - 1 && rect.End.X <= page.End.X + 1,
                            $"{kv.Key} sticks out of its page at {where}: {rect} in {page}");
                        Check(vp.Encloses(page), $"page off screen at {where}");
                    }
                    // Text is never smaller than 12 px in canvas units (scaled by the interface size on screen).
                    MinFont(_pages, where);
                }
            }
        _rt.Store.SetInterfaceSize(100);
        GetWindow().Size = new Vector2I(1280, 720);
        _notes.Add("fits(2 sizes x 3 interface sizes x 4 sections)");
    }

    private void MinFont(Node n, string where)
    {
        if (n is Label l && l.IsVisibleInTree())
            Check(l.GetThemeFontSize("font_size") >= 12, $"text '{l.Text}' smaller than 12 px at {where}");
        foreach (var c in n.GetChildren()) MinFont(c, where);
    }
}
