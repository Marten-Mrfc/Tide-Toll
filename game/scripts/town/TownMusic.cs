using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The music, from assets/audio/music/playlist.json: the first-age rotation in its order (the theme twice, it is
/// short; every other piece once), each piece fading in over 2 s and out over its last 3 s, then 1.5 s of silence
/// before the next. "Still Pages" while the book is closed (the rotation waits and both cross-fade over 1.2 s),
/// "Tide and Toll, Rising" once when the next age is rung in, then the rotation again.
/// Timed by its own clock from the pieces' lengths, so the state is the same with the Dummy driver (tests read
/// Current, Plays and Log); streams play only when sound is on.
/// </summary>
public partial class TownMusic : Node
{
    public const float FadeIn = 2f, FadeOut = 3f, Gap = 1.5f, PauseFade = 1.2f, RotationDb = -14f, PauseDb = -12f;
    public const string RisingId = "music-theme-rising", PauseId = "music-still-pages";

    private sealed class Piece { public string Id = "", Title = ""; public double Seconds; public AudioStream? Stream; public int Plays = 1; }

    private readonly List<Piece> _rotation = new();
    private Piece? _rising, _still;
    private AudioStreamPlayer _main = null!, _pausePlayer = null!;
    private bool _silent;
    private int _next;
    private Piece? _cur;
    private int _playsLeft;
    private double _t, _gapLeft = 3; // a few quiet seconds at the start
    private float _pauseMix; // 0 rotation, 1 pause piece
    private bool _paused;

    /// <summary>The piece now playing ("" in a gap), every piece started, in order (tests).</summary>
    public string Current => _paused ? PauseId : _cur?.Id ?? "";
    public string CurrentTitle => _paused ? _still?.Title ?? "" : _cur?.Title ?? "";
    public readonly List<string> Log = new();
    public int Starts { get; private set; }

    public void Init(bool silent)
    {
        Name = "Music";
        _silent = silent;
        _main = new AudioStreamPlayer { Name = "Rotation", Bus = "Music", VolumeDb = -80 };
        _pausePlayer = new AudioStreamPlayer { Name = "Pause", Bus = "Music", VolumeDb = -80 };
        AddChild(_main);
        AddChild(_pausePlayer);
        var byId = new Dictionary<string, Piece>();
        var order = new List<string>();
        const string path = "res://assets/audio/music/playlist.json";
        if (FileAccess.FileExists(path))
        {
            var root = Json.ParseString(FileAccess.GetFileAsString(path)).AsGodotDictionary();
            foreach (var p in root["pieces"].AsGodotArray())
            {
                var d = p.AsGodotDictionary();
                byId[d["id"].AsString()] = Make(d["id"].AsString(), d["title"].AsString(), d["seconds"].AsDouble(), "res://" + d["file"].AsString());
            }
            foreach (var id in root["rotation"].AsGodotArray()) order.Add(id.AsString());
        }
        else
        {
            // The same data, for an export without the playlist file.
            byId["theme-hamlet-draft"] = Make("theme-hamlet-draft", "Tide and Toll", 45, "res://assets/audio/music/theme-hamlet-draft.ogg");
            byId["music-ferry-morning"] = Make("music-ferry-morning", "Ferry Morning", 74.118, "res://assets/audio/music/music-ferry-morning.ogg");
            byId["music-long-field"] = Make("music-long-field", "The Long Field", 78.75, "res://assets/audio/music/music-long-field.ogg");
            byId["music-low-meadow"] = Make("music-low-meadow", "Low Meadow", 72, "res://assets/audio/music/music-low-meadow.ogg");
            byId[RisingId] = Make(RisingId, "Tide and Toll, Rising", 49.006, "res://assets/audio/music/music-theme-rising.ogg");
            byId[PauseId] = Make(PauseId, "Still Pages", 60, "res://assets/audio/music/music-still-pages.ogg");
            order.AddRange(new[] { "theme-hamlet-draft", "music-ferry-morning", "music-long-field", "music-low-meadow" });
        }
        foreach (var id in order)
            if (byId.TryGetValue(id, out var pc) && pc.Stream != null) { pc.Plays = pc.Seconds < 60 ? 2 : 1; _rotation.Add(pc); }
        byId.TryGetValue(RisingId, out _rising);
        byId.TryGetValue(PauseId, out _still);
        // The rotation plays each file through (no loop); the pause piece loops while the book stays closed.
        foreach (var pc in byId.Values)
            if (pc.Stream is AudioStreamOggVorbis ogg) ogg.Loop = pc == _still || pc.Plays > 1;
        if (_still?.Stream != null) _pausePlayer.Stream = _still.Stream;
    }

    private static Piece Make(string id, string title, double s, string res) =>
        new() { Id = id, Title = title, Seconds = s, Stream = ResourceLoader.Exists(res) ? GD.Load<AudioStream>(res) : null };

    /// <summary>The book closes (pause piece) or opens again (the rotation goes on where it was).</summary>
    public void SetPaused(bool on)
    {
        if (_paused == on) return;
        _paused = on;
        if (on) { Log.Add(PauseId); Starts++; if (!_silent && _still?.Stream != null) _pausePlayer.Play(); }
        if (!_silent) _main.StreamPaused = on && _cur != null;
    }

    /// <summary>The next age is rung in: the current piece fades quickly and "Rising" plays once.</summary>
    public void PlayRising()
    {
        if (_rising == null) return;
        Start(_rising, 1);
        _next = 0; // the rotation starts again from the theme afterwards
    }

    private void Start(Piece p, int plays)
    {
        _cur = p;
        _playsLeft = plays;
        _t = 0;
        Starts++;
        Log.Add(p.Id);
        if (Log.Count > 64) Log.RemoveAt(0);
        if (!_silent && p.Stream != null) { _main.Stream = p.Stream; _main.StreamPaused = false; _main.Play(); }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _pauseMix = Mathf.MoveToward(_pauseMix, _paused ? 1f : 0f, dt / PauseFade);
        if (!_silent)
        {
            _pausePlayer.VolumeDb = PauseDb + Mathf.LinearToDb(Math.Max(0.0001f, _pauseMix));
            if (_pauseMix <= 0f && _pausePlayer.Playing) _pausePlayer.Stop();
        }
        if (_paused) { if (!_silent) _main.VolumeDb = -80; return; }
        if (_cur == null)
        {
            _gapLeft -= delta;
            if (_gapLeft <= 0 && _rotation.Count > 0)
            {
                var p = _rotation[_next % _rotation.Count];
                _next = (_next + 1) % _rotation.Count;
                Start(p, p.Plays);
            }
            return;
        }
        _t += delta;
        double length = _cur.Seconds * _playsLeft;
        if (_t >= length)
        {
            _cur = null;
            _gapLeft = Gap;
            if (!_silent) _main.Stop();
            return;
        }
        if (!_silent)
        {
            float k = (float)Math.Min(Math.Min(1.0, _t / FadeIn), (length - _t) / FadeOut);
            k *= 1f - _pauseMix;
            _main.VolumeDb = RotationDb + Mathf.LinearToDb(Math.Max(0.0001f, Math.Clamp(k, 0f, 1f)));
        }
    }
}
