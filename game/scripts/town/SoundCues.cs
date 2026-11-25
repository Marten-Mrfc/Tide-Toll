using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The town's sound events as assets/audio/sfx/town-sounds.json lists them: files, intended level against the coin,
/// loop and pitch variation. Read once at start; the table below is the same data, used when the JSON cannot be read
/// (an export without it), so the game never goes silent for a missing data file.
/// The files are rendered at their intended loudness: every cue plays at the coin's gain (CoinDb), pitch varied by at
/// most 3 % where the file allows, never detuned otherwise.
/// </summary>
public sealed class SoundCue
{
    public string Id = "";
    public AudioStream[] Streams = Array.Empty<AudioStream>();
    public float VolumeDb;
    public bool Loop, PitchVary;
    /// <summary>Least real seconds between two plays (never a wall of the same sound).</summary>
    public double MinGap;
    public double LastAt = -1e9;
    public int Last = -1, Count;
}

public static class SoundCues
{
    public const string JsonPath = "res://assets/audio/sfx/town-sounds.json";

    // Events, their files (prefix, count) and suggested level, as in town-sounds.json on 2026-11-25.
    private static readonly (string id, string prefix, int n, float db, bool vary)[] Fallback =
    {
        ("upgrade-bought", "buy-0", 4, 1f, true), ("cannot-afford", "cannot-0", 2, -1f, true),
        ("level-reached", "level-up-0", 2, 3f, false), ("gift-arrives", "gift-0", 3, 2f, false),
        ("road-paved-gravel", "pave-gravel-0", 3, 1f, true), ("road-paved-cobbles", "pave-cobbles-0", 3, 1f, true),
        ("cottage-extended", "extend-0", 3, 2f, true), ("achievement", "achievement-0", 2, 3f, false),
        ("request-asked", "request-asked-0", 2, 2f, false), ("request-met", "request-met-0", 2, 2f, false),
        ("request-missed", "request-missed-0", 1, -1f, false), ("ui-press", "ui-press-0", 4, -4.5f, true),
        ("ui-hover", "ui-hover-0", 2, -9f, true), ("ui-tab", "ui-tab-0", 3, -4f, true),
        ("book-close", "book-close-0", 2, -1f, true), ("book-open", "book-open-0", 2, -1f, true),
        ("page-turn", "page-turn-0", 3, -3f, true), ("charter-granted", "charter-0", 1, 4f, false),
    };

    /// <summary>Least seconds between plays of one event (hover and presses can come fast; level sounds never overlap).</summary>
    private static double GapFor(string id) => id switch
    {
        "ui-hover" => 0.07, "ui-press" or "ui-tab" or "page-turn" => 0.05,
        "upgrade-bought" or "road-paved-gravel" or "road-paved-cobbles" or "cottage-extended" => 0.09,
        "cannot-afford" => 0.35, "achievement" => 1.2, "level-reached" or "gift-arrives" => 0.8,
        _ => 0.25,
    };

    public static Dictionary<string, SoundCue> Load()
    {
        var d = new Dictionary<string, SoundCue>();
        if (FileAccess.FileExists(JsonPath))
        {
            var root = Json.ParseString(FileAccess.GetFileAsString(JsonPath));
            if (root.VariantType == Variant.Type.Dictionary && root.AsGodotDictionary().TryGetValue("events", out var ev))
                foreach (var (k, v) in ev.AsGodotDictionary())
                {
                    var e = v.AsGodotDictionary();
                    var files = new List<AudioStream>();
                    foreach (var f in e["files"].AsGodotArray())
                    {
                        string p = "res://" + f.AsString();
                        if (ResourceLoader.Exists(p)) files.Add(GD.Load<AudioStream>(p));
                    }
                    if (files.Count == 0) continue;
                    string id = k.AsString();
                    d[id] = new SoundCue
                    {
                        Id = id, Streams = files.ToArray(), VolumeDb = (float)e["volume_db"].AsDouble(),
                        Loop = e.TryGetValue("loop", out var lp) && lp.AsBool(), PitchVary = e.TryGetValue("pitch_vary", out var pv) && pv.AsBool(),
                        MinGap = GapFor(id),
                    };
                }
        }
        foreach (var (id, prefix, n, db, vary) in Fallback)
        {
            if (d.ContainsKey(id)) continue;
            var files = new List<AudioStream>();
            for (int i = 1; i <= n; i++)
            {
                string p = $"res://assets/audio/sfx/{prefix}{i}.wav";
                if (ResourceLoader.Exists(p)) files.Add(GD.Load<AudioStream>(p));
            }
            if (files.Count > 0) d[id] = new SoundCue { Id = id, Streams = files.ToArray(), VolumeDb = db, PitchVary = vary, MinGap = GapFor(id) };
        }
        if (!d.ContainsKey("mill") && ResourceLoader.Exists("res://assets/audio/sfx/mill-loop.ogg"))
            d["mill"] = new SoundCue { Id = "mill", Streams = new[] { GD.Load<AudioStream>("res://assets/audio/sfx/mill-loop.ogg") }, VolumeDb = -6f, Loop = true };
        return d;
    }
}
