using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>Keys, Saving, reopening, the damaged file, the keyboard alone and the fit at several sizes.</summary>
public partial class SettingsTest
{
    private static bool Fires(string action, Key key)
    {
        var e = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true };
        return InputMap.EventIsAction(e, action, true);
    }

    private async Task Keys()
    {
        await Click("ribbon.Keys");
        Check(_pages.Section == "Keys", "Keys ribbon did not open Keys");
        Check(Fires("tt_pave", Godot.Key.P), "P should pave at first");

        // Rebind Pave to O, then use O.
        await Click("keys.tt_pave.0");
        Check(_pages.Capturing is { action: "tt_pave", slot: 0 }, "clicking a key did not wait for a new one");
        await Key(Godot.Key.O);
        Check(_pages.Capturing == null && _rt.Store.KeyFor("tt_pave", 0) == Godot.Key.O, "O was not bound to Pave");
        Check(((Button)C("keys.tt_pave.0")).Text == "O", "the list does not show O for Pave");
        await Key(Godot.Key.O, release: false);
        Check(Input.IsActionPressed("tt_pave"), "pressing O does not pave");
        Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.O, PhysicalKeycode = Godot.Key.O, Pressed = false });
        await Frames(2);
        Check(!Fires("tt_pave", Godot.Key.P), "P still paves after the rebind");

        // A clash: Space is the bell's. Swap.
        await Click("keys.tt_pave.0");
        await Key(Godot.Key.Space);
        Check(_pages.Slip == "clash", "no clash shown for Space");
        var text = FindLabel(_pages, "Ring the bell");
        Check(text != null, "the clash does not name Ring the bell");
        await Click("slip.0");
        Check(_rt.Store.KeyFor("tt_pave", 0) == Godot.Key.Space && _rt.Store.KeyFor("tt_bell", 0) == Godot.Key.O, "Swap keys did not swap");
        Check(Fires("tt_bell", Godot.Key.O) && Fires("tt_pave", Godot.Key.Space), "the InputMap does not follow the swap");

        // A clash cancelled with Esc: nothing changes, the pages stay open.
        await Click("keys.tt_bell.0");
        await Key(Godot.Key.A);
        Check(_pages.Slip == "clash", "no clash shown for A");
        await Key(Godot.Key.Escape);
        Check(_pages.Slip == "" && _rt.Store.KeyFor("tt_bell", 0) == Godot.Key.O && _rt.Store.KeyFor("tt_move_left", 0) == Godot.Key.A, "Esc did not cancel the clash");
        // Esc cancels a rebind.
        await Click("keys.tt_home_view.0");
        await Key(Godot.Key.Escape);
        Check(_pages.Capturing == null && _rt.Store.KeyFor("tt_home_view", 0) == Godot.Key.Home, "Esc did not cancel the rebind");
        Check(_closed == 0 && _pages.IsInsideTree(), "Esc during a rebind closed the pages");
        // The empty second slot can take a key.
        await Click("keys.tt_bell.1");
        await Key(Godot.Key.B);
        Check(_rt.Store.KeyFor("tt_bell", 1) == Godot.Key.B && Fires("tt_bell", Godot.Key.B), "B in the bell's second slot");

        // Round 4 (A4): Ring in the next age is a listed, rebindable action with N, and N clashes with it.
        Check(_pages.Controls.ContainsKey("keys.tt_next_age.0") && FindLabel(_pages, "Ring in the next age") != null, "Ring in the next age is not on the Keys page");
        Check(Fires("tt_next_age", Godot.Key.N), "N should ring in the next age at first");
        await Click("keys.tt_bell.0");
        await Key(Godot.Key.N);
        Check(_pages.Slip == "clash" && FindLabel(_pages, "Ring in the next age") != null, "binding N to the bell does not ask about Ring in the next age");
        await Key(Godot.Key.Escape);
        Check(_rt.Store.KeyFor("tt_next_age", 0) == Godot.Key.N, "the clash cancelled changed the next-age key");
        await Click("keys.tt_next_age.0");
        await Key(Godot.Key.J);
        Check(Fires("tt_next_age", Godot.Key.J) && !Fires("tt_next_age", Godot.Key.N), "Ring in the next age could not be rebound to J");

        await Click("keys.restore");
        Check(_rt.Store.KeyFor("tt_pave", 0) == Godot.Key.P && _rt.Store.KeyFor("tt_bell", 0) == Godot.Key.Space
              && _rt.Store.KeyFor("tt_bell", 1) == Godot.Key.None, "Restore defaults");
        Check(Fires("tt_pave", Godot.Key.P) && !Fires("tt_pave", Godot.Key.Space), "the InputMap after Restore defaults");
        // Leave one rebind in place for the reopen check.
        await Click("keys.tt_zoom_in.0");
        await Key(Godot.Key.I);
        Check(_rt.Store.KeyFor("tt_zoom_in", 0) == Godot.Key.I, "I for zoom in");
        _notes.Add("keys");
    }

    private static Label? FindLabel(Node n, string part)
    {
        if (n is Label l && l.IsVisibleInTree() && l.Text.Contains(part)) return l;
        foreach (var c in n.GetChildren())
            if (FindLabel(c, part) is { } found) return found;
        return null;
    }

    private async Task Saving()
    {
        await Click("ribbon.Saving");
        Check(_pages.Section == "Saving", "Saving ribbon did not open Saving");
        Check(FindLabel(_pages, "Last saved at 20:31") != null, "the last save time is not shown");
        Check(FindLabel(_pages, _saves.FolderPath) != null, "the save folder is not shown");
        await Click("saving.autosave", 0.9f);
        Check(_rt.Store.AutosaveMinutes == 5, $"autosave every 5 minutes (got {_rt.Store.AutosaveMinutes})");
        await Click("saving.save");
        Check(_saves.Saved == 1 && FindLabel(_pages, "Last saved at 21:05") != null, "Save now");
        await Click("saving.load");
        Check(_saves.Loaded == 1 && FindLabel(_pages, "The last save is loaded.") != null, "Load the last save");
        await Click("saving.delete");
        Check(_pages.Slip == "delete" && _saves.Deleted == 0, "Delete did not ask first");
        await Click("slip.1");
        Check(_pages.Slip == "" && _saves.Deleted == 0, "Keep the save deleted it");
        await Click("saving.delete");
        await Click("slip.0");
        Check(_saves.Deleted == 1 && !_saves.HasSave, "Delete the save");
        Check(((Button)C("saving.load")).Disabled && ((Button)C("saving.delete")).Disabled, "load and delete should be off with no save");
        Check(FindLabel(_pages, "Not saved yet") != null, "no save should read Not saved yet");
        _notes.Add("saving");
    }

    private async Task Reopen()
    {
        await Click("back");
        Check(_closed == 1, "Back did not raise Closed");
        await CloseAll();
        // Scramble the live state so the reopen has to bring it back from the file.
        GameInput.Register();
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Music"), 0);
        GetWindow().ContentScaleFactor = 1f;
        Open(new RecordingDisplay(), new FakeSaves());
        await Frames(4);
        var st = _rt.Store;
        Check(st.LoadedFromFile, "the file was not read on reopen");
        Check(st.Volume("Effects") == 100 && st.Volume("Music") > 10 && st.Volume("Music") < 50 && st.MuteWhenInactive, "sound settings not kept");
        Near(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Music")), SettingsApplier.VolumeDb(st.Volume("Music")), "Music bus after reopen");
        Check(st.WindowMode == WindowMode.Windowed && st.Resolution == new Vector2I(1366, 768), "window not kept");
        Check(_display.Size == new Vector2I(1366, 768), "the window size was not applied at start-up");
        Check(!st.Vsync && st.FrameLimit == 60 && _display.Fps == 60 && !_display.Vsync, "vsync or frame limit not kept and applied");
        Check(st.InterfaceSize == 110, "interface size not kept");
        Near(GetWindow().ContentScaleFactor, 1.1f, "content scale at start-up");
        Check(!st.ShowCoinNumbers && !st.FollowTown && st.AutosaveMinutes == 5, "display or saving switches not kept");
        Check(st.KeyFor("tt_zoom_in", 0) == Godot.Key.I && Fires("tt_zoom_in", Godot.Key.I), "the rebind was not kept and applied");
        Check(_pages.Section == "Sound" && ((HSlider)C("sound.Effects")).Value == 100, "the reopened pages do not show the values");
        _notes.Add("reopen");
    }

    private void Damaged()
    {
        const string path = "user://settings-damaged.cfg";
        var rng = new RandomNumberGenerator { Seed = 7 };
        var junk = new byte[600];
        for (int i = 0; i < junk.Length; i++) junk[i] = (byte)rng.RandiRange(0, 255);
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write)) f.StoreBuffer(junk);
        var s = GameSettings.Load(path);
        var d = new GameSettings(path);
        Check(!s.LoadedFromFile, "a file of junk should read as nothing");
        Check(Same(s, d), "a file of junk should load as defaults");
        // A half-damaged file keeps what it can read.
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write))
            f.StoreString("[sound]\nmaster=banana\nmusic=40\neffects=900\n[display\nwindow_mode=\"sideways\"\ninterface_size=130\n[keys]\ntt_bell=\"Q,Nonsense\"\ntt_pave=\"K,\"\n");
        s = GameSettings.Load(path);
        Check(s.Volume("Master") == 80 && s.Volume("Music") == 40 && s.Volume("Effects") == 80, "half-damaged sound section");
        Check(s.WindowMode == WindowMode.Windowed && s.InterfaceSize == 100, "lines after a broken section header should be ignored");
        Check(s.KeyFor("tt_bell", 0) == Godot.Key.Space && s.KeyFor("tt_pave", 0) == Godot.Key.K, "keys from a half-damaged file");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        _notes.Add("damaged");
    }

    private static bool Same(GameSettings a, GameSettings b)
    {
        foreach (var bus in GameSettings.Buses) if (a.Volume(bus) != b.Volume(bus)) return false;
        foreach (var act in GameInput.Actions)
            for (int i = 0; i < GameInput.Slots; i++) if (a.KeyFor(act.Name, i) != b.KeyFor(act.Name, i)) return false;
        return a.MuteWhenInactive == b.MuteWhenInactive && a.WindowMode == b.WindowMode && a.Resolution == b.Resolution
            && a.Vsync == b.Vsync && a.FrameLimit == b.FrameLimit && a.InterfaceSize == b.InterfaceSize
            && a.ShowCoinNumbers == b.ShowCoinNumbers && a.FollowTown == b.FollowTown && a.AutosaveMinutes == b.AutosaveMinutes;
    }
}
