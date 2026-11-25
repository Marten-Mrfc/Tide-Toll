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

    protected override async Task Run()
    {
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
        string v1 = v2.Replace("\\\"Version\\\":" + TownSave.CurrentVersion, "\\\"Version\\\":1")
                      .Replace("\\u0022Version\\u0022:" + TownSave.CurrentVersion, "\\u0022Version\\u0022:1");
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

        // Delete the save (the Saving page's action after it confirms): a new town; the settings stay.
        var store = Settings.GameSettings.Current!;
        store.SetVolume("Effects", 33);
        int seed2 = Main.Driver.Seed;
        Main.Saver.DeleteSave();
        Check(Main.Driver.Seed != seed2 && T.Level == 1, "deleting the save did not start a new town");
        Check(store.Volume("Effects") == 33 && FileAccess.FileExists(Main.Profile + "settings.cfg"), "deleting the save touched the settings");
        // Load the last save from the page.
        Check(Main.Saver.LoadLast(), "LoadLast failed");
    }
}
