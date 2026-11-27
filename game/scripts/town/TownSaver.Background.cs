using System;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game;

/// <summary>
/// n10 (Q7): the autosave's write and rename on a worker thread. The bytes are made on the main thread (SaveNow); the
/// worker writes them to the .tmp file and renames it over the save, as the main thread did; the main thread takes the
/// result in a later frame (<see cref="Collect"/>) or waits for it before any other save, load or delete.
/// </summary>
public partial class TownSaver
{
    private Task<string?>? _writing;
    /// <summary>Saves whose file was written on the worker (tests).</summary>
    public int BackgroundSaves { get; private set; }
    /// <summary>A write is still running on the worker (tests).</summary>
    public bool Writing => _writing != null && !_writing.IsCompleted;

    private bool WriteInBackground(byte[] bytes, string tmpAbs, string pathAbs)
    {
        _writing = Task.Run(() =>
        {
            try
            {
                File.WriteAllBytes(tmpAbs, bytes);
                File.Move(tmpAbs, pathAbs, overwrite: true);
                return (string?)null;
            }
            catch (Exception e)
            {
                try { if (File.Exists(tmpAbs)) File.Delete(tmpAbs); } catch (Exception) { }
                return e.Message;
            }
        });
        return true;
    }

    /// <summary>Waits for a write still running and takes its result.</summary>
    private void FinishWrite()
    {
        if (_writing == null) return;
        _writing.Wait();
        Collect();
    }

    private void Collect()
    {
        var w = _writing!;
        _writing = null;
        string? error = w.Result;
        if (error == null)
        {
            Saves++;
            BackgroundSaves++;
            _failNoted = false;
            return;
        }
        GD.Print("Could not save the town: " + error);
        Failed("The town could not be saved: the file could not be written.");
    }
}
