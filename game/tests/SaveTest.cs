using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Saving: the town played at speed to level 2 with a road paved, saved, and the scene loaded again: coin, level,
/// villagers, road wear and paving, the tick and the camera are the same, and the town runs on. A damaged save gives a
/// new town, a margin note and the file set aside. The quit paths (the menu's Quit, the window's close button) write
/// a save. Deleting the save from the Saving page starts a new town and leaves the settings alone.
/// </summary>
public partial class SaveTest : TownTestBase
{
    protected override string Marker => "SAVE PASS";
    protected override Vector2I WindowSize => new(1920, 1080);

    /// <summary>Loads the scene again; returns the town's state as it was loaded, before any frame runs it on.</summary>
    private async Task<string> Reload()
    {
        Main.QueueFree();
        await Frames(3);
        Main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        AddChild(Main);
        string loaded = State(T);
        await Frames(5);
        return loaded;
    }

    private static string State(Town t)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"tick {t.Tick} coin {t.Coin:R} level {t.Level} renown {t.Renown:R} villagers {t.VillagerCount} paved {t.RoadsPaved};");
        for (int i = 0; i < t.VillagerCount; i++) { var v = t.Villager(i); sb.Append($" v{i} {v.X:R},{v.Y:R},{v.Phase}"); }
        for (int r = 0; r < t.RoadCount; r++) { var w = t.RoadInfo(r); sb.Append($" r{r} {w.Open} {w.Wear:R} {w.Surface}"); }
        return sb.ToString();
    }

    private async Task ClickC(Control c)
    {
        var p = GetViewport().GetFinalTransform() * c.GetGlobalRect().GetCenter();
        await Click(p);
        await Frames(2);
    }

    /// <summary>Round 4 (A6): the autosave clock counts real time, also when the engine's frame time runs slow.</summary>
    private async Task AutosaveCountsRealTime()
    {
        Check((Settings.GameSettings.Current?.AutosaveMinutes ?? 0) > 0, "autosave is on by default");
        await Frames(2);
        double c0 = Main.Saver.Clock; ulong t0 = Time.GetTicksMsec();
        Engine.TimeScale = 0.1; // every frame's delta is a tenth of the real time, as on a machine the engine clamps
        for (int i = 0; i < 40; i++) { OS.DelayMsec(15); await Frames(1); }
        Engine.TimeScale = 1;
        double real = (Time.GetTicksMsec() - t0) / 1000.0, counted = Main.Saver.Clock - c0;
        Check(counted > real * 0.8 && counted < real * 1.2, $"the autosave counted {counted:0.00} s in {real:0.00} s of real time");
        Notes.Add($"autosave-clock={counted:0.00}/{real:0.00}s");
    }

    protected override async Task Run()
    {
        await AutosaveCountsRealTime();
        // Play at speed to level 2 with a road paved.
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.SetSpeed(64);
        Main.Driver.RunBot(bot, t => t.Level >= 2 && t.RoadsPaved >= 1, 10 * 60 * 60);
        Main.Driver.SetSpeed(1);
        Check(T.Level >= 2 && T.RoadsPaved >= 1, $"the bot did not reach level 2 with a paved road (level {T.Level}, paved {T.RoadsPaved})");
        await Frames(5); // the level's gift is drained and makes room first (the view follows again)
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal + new Vector3(-6, 0, 4), 70f, instant: true);
        await Frames(5);
        Main.Driver.SetPaused(true);
        await Frames(2);
        Check(Main.Saver.SaveNow(), "SaveNow failed");
        Check(Main.Saver.LastSaveTime != null, "no time for the last save");
        // n10 (Q7): the autosave writes on a worker; its file is byte for byte the one a save on the main thread writes,
        // and it is the file the reload below loads.
        byte[] onMain = FileAccess.GetFileAsBytes(Main.Saver.SavePath);
        int saves0 = Main.Saver.Saves, bg0 = Main.Saver.BackgroundSaves;
        Check(Main.Saver.SaveNow(background: true), "the background save did not start");
        await Until(() => !Main.Saver.Writing && Main.Saver.Saves > saves0, 600, "the background save to finish");
        Check(Main.Saver.BackgroundSaves == bg0 + 1, "the save was not written on the worker");
        byte[] onWorker = FileAccess.GetFileAsBytes(Main.Saver.SavePath);
        Check(onMain.Length > 1000 && onMain.AsSpan().SequenceEqual(onWorker), $"the worker's save differs from the main thread's ({onMain.Length} and {onWorker.Length} bytes)");
        Notes.Add($"background save {onWorker.Length} bytes, identical;");
        string before = State(T);
        int seed = Main.Driver.Seed;
        var pivot = Main.Rig.PivotLocal; float size = Main.Rig.Camera.Size;

        string after = await Reload();
        Check(after == before, "the reloaded town differs:\n" + before + "\n" + after);
        Check(Main.Driver.Seed == seed, "the seed changed");
        Check(Main.Rig.UserMoved && Main.Rig.PivotLocal.DistanceTo(pivot) < 0.5f && MathF.Abs(Main.Rig.Camera.Size - size) < 0.5f,
            $"the camera was not restored: {Main.Rig.PivotLocal} {Main.Rig.Camera.Size} (saved {pivot} {size})");
        long tick = T.Tick;
        await Frames(30);
        Check(T.Tick > tick, "the reloaded town does not run on");
        Notes.Add($"level {T.Level} paved {T.RoadsPaved} villagers {T.VillagerCount}");

        // Round 4 (A7): pause by Esc, then the process ends without a clean quit: the town opens at the view the
        // player had before pausing, not the closed book's.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal + new Vector3(5, 0, -3), 64f, instant: true);
        await Frames(5);
        var pivotP = Main.Rig.PivotLocal; float sizeP = Main.Rig.Camera.Size;
        await Key(Godot.Key.Escape);
        await Until(() => Main.Menu.CoverShown, 600, "the cover menu");
        await Reload();
        Check(Main.Rig.UserMoved && Main.Rig.PivotLocal.DistanceTo(pivotP) < 0.5f && MathF.Abs(Main.Rig.Camera.Size - sizeP) < 0.5f,
            $"after a pause and an unclean end the view is {Main.Rig.PivotLocal} size {Main.Rig.Camera.Size} (was {pivotP} size {sizeP})");

        // Round 4 (A9): a save that cannot be written says so; a read that fails sets nothing aside and writes nothing over it.
        string tmpPath = Main.Saver.SavePath + ".tmp";
        DirAccess.MakeDirRecursiveAbsolute(tmpPath); // a folder where the save is written first: the write fails
        Check(!Main.Saver.SaveNow(), "a save that could not be written reported success");
        Check(Main.Hud.LastNote.StartsWith("The town could not be saved"), $"no note for a save that could not be written ('{Main.Hud.LastNote}')");
        int fails0 = Main.Saver.Failures;
        Main.Saver.SaveNow(background: true);   // n10: the worker's failure reaches the main thread as a failure too
        await Until(() => !Main.Saver.Writing && Main.Saver.Failures > fails0, 600, "the background save's failure");
        DirAccess.RemoveAbsolute(tmpPath);
        Check(Main.Saver.SaveNow(), "saving works again");
        string good = FileAccess.GetFileAsString(Main.Saver.SavePath);
        int goodSeed = Main.Driver.Seed;
        TownSaver.ReadSeam = _ => null;
        try { await Reload(); } finally { TownSaver.ReadSeam = null; }
        Check(FileAccess.FileExists(Main.Saver.SavePath) && Main.Saver.SetAside == "", "a save that failed to read was set aside");
        Check(Main.StartNote.Contains("could not be read from the disk"), $"no note for a failed read ('{Main.StartNote}')");
        Check(!Main.Saver.SaveNow() && FileAccess.GetFileAsString(Main.Saver.SavePath) == good, "the new town was saved over a save that failed to read");
        await Reload();
        Check(Main.Driver.Seed == goodSeed && Main.StartNote == "", "the good save did not load once the disk read it again");

        // A damaged save: a new town, a note, the file set aside.
        string path = Main.Saver.SavePath;
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write)) f.StoreString("{\"Game\":1,\"Town\":\"{not a town");
        await Reload();
        Check(T.Level == 1 && T.Tick < 100, $"a damaged save did not give a new town (level {T.Level}, tick {T.Tick})");
        Check(Main.StartNote != "" && Main.Hud.NoteLog.Contains(Main.StartNote), "no margin note about the damaged save");
        Check(Main.Saver.SetAside != "" && FileAccess.FileExists(Main.Saver.SetAside), "the damaged file was not set aside");
        Check(!FileAccess.FileExists(path), "the damaged file is still in the save's place");
        // Another version is set aside too.
        Main.Saver.SaveNow();
        string text = FileAccess.GetFileAsString(path).Replace("\"Game\":1", "\"Game\":99");
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write)) f.StoreString(text);
        await Reload();
        Check(Main.StartNote != "" && !FileAccess.FileExists(path), "a save from another version was not set aside");
        // Round 3: a save from the first rules (the town's own save version 1) is set aside like any unreadable save.
        Main.Saver.SaveNow();
        string v2 = FileAccess.GetFileAsString(path);
        // Round 4: a first-age town saves as TownSave.FirstAgeVersion (2); only a town in the second age writes CurrentVersion (3).
        int written = T.InRiverAge ? TownSave.CurrentVersion : TownSave.FirstAgeVersion;
        string v1 = v2.Replace("\\\"Version\\\":" + written, "\\\"Version\\\":1")
                      .Replace("\\u0022Version\\u0022:" + written, "\\u0022Version\\u0022:1");
        Check(v1 != v2, "the test could not find the town's save version in the file");
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write)) f.StoreString(v1);
        await Reload();
        Check(T.Level == 1 && T.Tick < 100 && Main.StartNote != "" && !FileAccess.FileExists(path), "a save from the first rules was not set aside");

        // The quit paths write a save: the window's close button and the menu's Quit.
        Main.HoldQuit = true;
        Main.Notification((int)NotificationWMCloseRequest);
        Check(Main.QuitRequests == 1 && FileAccess.FileExists(path), "the close button did not save");
        DirAccess.RemoveAbsolute(path);
        await Key(Godot.Key.Escape);
        await Until(() => Main.Menu.CoverShown, 600, "the cover menu");
        DirAccess.RemoveAbsolute(path); // pausing saved; Quit must save again
        Main.Menu.QuitButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(Main.QuitRequests == 2 && FileAccess.FileExists(path), "Quit did not save");

        // Delete the save on the Saving page with real clicks (round 4, A5): a new town, the settings stay, and no save is
        // left: the page says so and offers no Load until the new town is saved (pausing saves it).
        var store = Settings.GameSettings.Current!;
        store.SetVolume("Effects", 33);
        int seed2 = Main.Driver.Seed;
        Check(Main.Menu.CoverShown, "the cover menu after Quit was held");
        Main.Menu.SettingsButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(3);
        var pages = Main.Menu.Pages!;
        pages.ShowSection("Saving");
        await Frames(3);
        await ClickC(pages.Controls["saving.delete"]);
        await ClickC(pages.Controls["slip.0"]);
        Check(Main.Driver.Seed != seed2 && T.Level == 1, "deleting the save did not start a new town");
        Check(store.Volume("Effects") == 33 && FileAccess.FileExists(Main.Profile + "settings.cfg"), "deleting the save touched the settings");
        Check(!Main.Saver.HasSave, "Delete left a save");
        Check(((Button)pages.Controls["saving.load"]).Disabled, "Load is offered after Delete");
        // Changed in round 4 (A5): there is no save after Delete, so loading finds none (it loaded the new town before).
        Check(!Main.Saver.LoadLast(), "LoadLast found a save after Delete");
        await ClickC(pages.Controls["back"]);
        await Key(Godot.Key.Escape);
        await Until(() => Main.Menu.Now == PauseMenu.State.Playing, 600, "the book open again");
        Check(!Main.Saver.HasSave, "Resume wrote a save");
        await Key(Godot.Key.Escape);
        await Until(() => Main.Menu.CoverShown, 600, "the cover menu");
        Check(Main.Saver.HasSave, "pausing the new town did not save it");
        Check(Main.Saver.LoadLast(), "LoadLast failed");
    }
}
