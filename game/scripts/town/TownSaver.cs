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
    private ulong _lastMs;
    private bool _readFailed, _failNoted;
    /// <summary>Real seconds counted toward the next autosave (tests).</summary>
    public double Clock => _clock;
    /// <summary>Tests: reads the save's bytes instead of the disk (null: the read failed).</summary>
    public static Func<string, byte[]?>? ReadSeam;
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

    public override void _ExitTree()
    {
        FinishWrite();
        if (GameSettings.Current != null) GameSettings.Current.Changed -= OnSetting;
    }

    private void OnSetting(string key) { if (key == GameSettings.KAutosave) _clock = 0; }

    public override void _Process(double delta)
    {
        // Round 4: real time, not the frame's delta (the engine clamps and scales that on slow frames).
        if (_writing != null && _writing.IsCompleted) Collect();
        ulong now = Time.GetTicksMsec();
        double dt = _lastMs == 0 ? 0 : (now - _lastMs) / 1000.0;
        _lastMs = now;
        int minutes = GameSettings.Current?.AutosaveMinutes ?? 2;
        if (minutes <= 0 || _main.Driver.Paused) return;
        _clock += dt;
        if (_clock >= minutes * 60.0) { _clock = 0; SaveNow(background: true); }
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

    public bool SaveNow() => SaveNow(false);

    /// <summary>
    /// Writes the save. n10 (performance research Q7): with <paramref name="background"/> (the autosave) the town and
    /// the file's bytes are made here, on the main thread, exactly as for any save, and only the writing and the rename
    /// happen on a worker; the result (Saves, or a failure's note) is taken in a later frame. Every other save (pause,
    /// quit, a new town, the Saving page) first waits for a write still running, then writes as before.
    /// </summary>
    public bool SaveNow(bool background)
    {
        FinishWrite();
        if (_readFailed) Settings.GameSettings.Current?.SaveHintsIfChanged(); // the settings are still written
        if (_readFailed) return Failed("The save on disk could not be read, so it is not written over. Restart the game to try it again.");
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
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(f);   // the same bytes StoreString wrote from Serialize
            if (background)
            {
                _clock = 0;
                // The settings kept in memory (hints seen, the readout's F3) are written here, in the autosave's own frame
                // on the main thread (the store is the main thread's), as the save on the main thread writes them below.
                Settings.GameSettings.Current?.SaveHintsIfChanged();
                return WriteInBackground(bytes, ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(SavePath));
            }
            using (var fa = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
            {
                if (fa == null) return Failed("The town could not be saved: the file could not be written.");
                fa.StoreBuffer(bytes);
            }
            if (DirAccess.RenameAbsolute(tmp, SavePath) != Error.Ok)
            {
                DirAccess.RemoveAbsolute(tmp);
                return Failed("The town could not be saved: the file could not be written.");
            }
            Saves++;
            _clock = 0;
            // First-minute hints seen and the readout's F3 choice since the last save go to the settings file with the town
            // (never in a frame of their own).
            Settings.GameSettings.Current?.SaveHintsIfChanged();
            _failNoted = false;
            return true;
        }
        catch (Exception e)
        {
            GD.Print("Could not save the town: " + e.Message);
            return Failed("The town could not be saved.");
        }
    }

    /// <summary>Saves that failed (round 4: a failed save says so in a note, once until a save works again).</summary>
    public int Failures { get; private set; }

    private bool Failed(string note)
    {
        Failures++;
        _clock = 0;
        if (!_failNoted && _main.Hud != null) { _failNoted = true; _main.Hud.Trouble(note); }
        return false;
    }

    /// <summary>The save's bytes, read completely, or null when the read itself failed.</summary>
    private byte[]? ReadAll()
    {
        if (ReadSeam != null) return ReadSeam(SavePath);
        using var fa = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (fa == null) return null;
        ulong len = fa.GetLength();
        var bytes = fa.GetBuffer((long)len);
        return (ulong)bytes.Length == len ? bytes : null;
    }

    /// <summary>
    /// Continues the saved town if there is one. Returns false when there is none or it could not be read; then
    /// <paramref name="note"/> says so for the margin ("" when there simply was no save).
    /// </summary>
    public bool TryContinue(out string note)
    {
        FinishWrite();
        note = "";
        if (!HasSave) return false;
        // Round 4: a read that fails (the file could not be read in full) never sets a good save aside: it stays where
        // it is, the new town is not saved over it, and a note says so.
        var bytes = ReadAll();
        if (bytes == null)
        {
            _readFailed = true;
            note = "The saved town could not be read from the disk, so a new town begins. The save is left as it was.";
            return false;
        }
        SaveFile f;
        Town town;
        try
        {
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            f = JsonSerializer.Deserialize<SaveFile>(text) ?? throw new InvalidOperationException("empty save");
            if (f.Game != GameVersion) throw new InvalidOperationException("another version");
            town = TownSave.Load(f.Town);
        }
        catch (Exception)
        {
            SetAside = Main.Profile + $"town-unreadable-{(long)Time.GetUnixTimeFromSystem()}.json";
            DirAccess.RenameAbsolute(SavePath, SetAside);
            note = "The saved town could not be read, so a new town begins. The old file is kept beside the save.";
            return false;
        }
        // A loaded town is not news: drop what the load queued (level and arrival events), so no gift note plays
        // again and the camera stays where it was saved.
        town.DrainEvents(new System.Collections.Generic.List<TownEvent>());
        _main.Driver.Adopt(town);
        _main.RestoreView(new Vector3(f.CamX, 0, f.CamZ), f.CamSize, f.UserMoved);
        _readFailed = false;
        return true;
    }

    public bool LoadLast()
    {
        FinishWrite();
        if (!HasSave) return false;
        bool ok = TryContinue(out string note);
        if (!ok && note != "") { _main.NewTown(); _main.Hud.Trouble(note); }
        return ok;
    }

    /// <summary>
    /// Removes the save and starts a new town. Round 4: the new town is not written at once, so the page says there is
    /// no save (and offers no Load) until the next autosave, pause or quit.
    /// </summary>
    public void DeleteSave()
    {
        FinishWrite();
        _readFailed = false;
        _main.NewTown();
        if (HasSave) DirAccess.RemoveAbsolute(SavePath);
        _clock = 0;
    }
}
