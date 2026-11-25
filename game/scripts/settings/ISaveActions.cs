using System;

namespace TideToll.Game.Settings;

/// <summary>
/// What the Saving page needs from the game. The game implements it and passes it to SettingsPages; the settings
/// store only keeps the autosave choice (GameSettings.AutosaveMinutes), the game does the saving.
/// </summary>
public interface ISaveActions
{
    /// <summary>True when there is a save on disk to load or delete.</summary>
    bool HasSave { get; }
    /// <summary>When the save on disk was written (local time), or null when there is none.</summary>
    DateTime? LastSaveTime { get; }
    /// <summary>The folder that holds the save, as a full path the player can find (ProjectSettings.GlobalizePath).</summary>
    string FolderPath { get; }
    /// <summary>Saves now; returns false if it could not.</summary>
    bool SaveNow();
    /// <summary>Loads the last save; returns false if there is none or it could not be read.</summary>
    bool LoadLast();
    /// <summary>Deletes the save on disk.</summary>
    void DeleteSave();
}
