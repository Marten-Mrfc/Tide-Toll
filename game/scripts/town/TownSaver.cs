using System;
using System.Text.Json;
using Godot;
using TideToll.Game.Settings;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Saves the town by itself: every few minutes as the settings say (GameSettings.AutosaveMinutes, 0 = off), on pause
/// and on quit, through the simulation's TownSave, to town.json in the profile folder (user:// in the game, a folder
/// of its own in each test). The file holds the game's own version, the camera and the town's save. On start Main
/// continues it; a damaged file or one from another version is renamed aside and a new town begins. Also the Saving
/// page's ISaveActions.
/// </summary>
public partial class TownSaver : Node, ISaveActions
{
    public const int GameVersion = 1;
    public const string FileName = "town.json";

    private Main _main = null!;
    private double _clock;
    public int Saves { get; private set; }
    /// <summary>Path of the file the last damaged save was moved to ("" when none).</summary>
    public string SetAside { get; private set; } = "";

    public string SavePath => Main.Profile + FileName;

    private sealed class SaveFile
    {
        public int Game { get; set; }
        public float CamX { get; set; }
        public float CamZ { get; set; }
        public float CamSize { get; set; }
        public bool UserMoved { get; set; }
        public string Town { get; set; } = "";
    }

    public void Init(Main main)
    {
        _main = main;
        Name = "Saver";
        if (GameSettings.Current != null) GameSettings.Current.Changed += OnSetting;
    }

    public override void _ExitTree() { if (GameSettings.Current != null) GameSettings.Current.Changed -= OnSetting; }

    private void OnSetting(string key) { if (key == GameSettings.KAutosave) _clock = 0; }

    public override void _Process(double delta)
    {
        int minutes = GameSettings.Current?.AutosaveMinutes ?? 2;
        if (minutes <= 0 || _main.Driver.Paused) return;
        _clock += delta;
        if (_clock >= minutes * 60.0) { _clock = 0; SaveNow(); }
    }

    public bool HasSave => FileAccess.FileExists(SavePath);

    public DateTime? LastSaveTime
    {
        get
        {
            if (!HasSave) return null;
            ulong unix = FileAccess.GetModifiedTime(SavePath);
            return DateTimeOffset.FromUnixTimeSeconds((long)unix).LocalDateTime;
        }
    }

    public string FolderPath => ProjectSettings.GlobalizePath(Main.Profile);

    public bool SaveNow()
    {
        try
        {
            var (pivot, size, moved) = _main.ViewToKeep();
            var f = new SaveFile
            {
                Game = GameVersion, CamX = pivot.X, CamZ = pivot.Z, CamSize = size, UserMoved = moved,
                Town = TownSave.Save(_main.Driver.Town),
            };
            DirAccess.MakeDirRecursiveAbsolute(Main.Profile);
            string tmp = SavePath + ".tmp";
            using (var fa = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
            {
                if (fa == null) return false;
                fa.StoreString(JsonSerializer.Serialize(f));
            }
            if (DirAccess.RenameAbsolute(tmp, SavePath) != Error.Ok) return false;
            Saves++;
            _clock = 0;
            return true;
        }
        catch (Exception e)
        {
            GD.Print("Could not save the town: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// Continues the saved town if there is one. Returns false when there is none or it could not be read; then
    /// <paramref name="note"/> says so for the margin ("" when there simply was no save).
    /// </summary>
    public bool TryContinue(out string note)
    {
        note = "";
        if (!HasSave) return false;
        try
        {
            string text = FileAccess.GetFileAsString(SavePath);
            var f = JsonSerializer.Deserialize<SaveFile>(text) ?? throw new InvalidOperationException("empty save");
            if (f.Game != GameVersion) throw new InvalidOperationException("another version");
            var town = TownSave.Load(f.Town);
            // A loaded town is not news: drop what the load queued (level and arrival events), so no gift note plays
            // again and the camera stays where it was saved.
            town.DrainEvents(new System.Collections.Generic.List<TownEvent>());
            _main.Driver.Adopt(town);
            _main.RestoreView(new Vector3(f.CamX, 0, f.CamZ), f.CamSize, f.UserMoved);
            return true;
        }
        catch (Exception)
        {
            SetAside = Main.Profile + $"town-unreadable-{(long)Time.GetUnixTimeFromSystem()}.json";
            DirAccess.RenameAbsolute(SavePath, SetAside);
            note = "The saved town could not be read, so a new town begins. The old file is kept beside the save.";
            return false;
        }
    }

    public bool LoadLast()
    {
        if (!HasSave) return false;
        bool ok = TryContinue(out string note);
        if (!ok && note != "") { _main.NewTown(); _main.Hud.Note(note, strong: true); }
        return ok;
    }

    public void DeleteSave()
    {
        if (HasSave) DirAccess.RemoveAbsolute(SavePath);
        _main.NewTown();
    }
}
