using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The town's sound. Every event in assets/audio/sfx/town-sounds.json plays its own files (SoundCues) at the coin's
/// gain plus nothing (the files are rendered at their intended loudness), never the same file twice running, pitch
/// varied only where the file allows, and never more often than its least gap. World sounds go to the Effects bus,
/// the interface and the book to the Interface bus, music to Music (TownMusic), the river and the mill to Ambience.
/// The mill's loop is heard while the mill works, louder near the view's centre and fading with distance and zoom.
/// Nothing plays when headless or with the Dummy driver, but every cue is still counted (tests).
/// </summary>
public partial class TownAudio : Node
{
    /// <summary>The coin's player gain: the reference every town sound is rendered against.</summary>
    public const float CoinDb = -12f;
    public bool Silent { get; private set; }
    public int Coins, Purchases, Levels, Bells, Steps, Paves, Achievements, Requests, Extends, UiSounds, BookSounds, Cannots, Charters;
    /// <summary>The mill loop's current linear level (0 silent .. 1 full), for tests.</summary>
    public float MillLevel { get; private set; }
    public bool PauseMusic { get; private set; }
    public TownMusic Music { get; private set; } = null!;
    /// <summary>Every cue played, by event id (tests).</summary>
    public readonly Dictionary<string, int> Played = new();

    private TownDriver _drv = null!;
    private Dictionary<string, SoundCue> _cues = null!;
    private AudioStream[] _coin = null!, _bell = null!, _long = null!, _grass = null!, _dirt = null!;
    private AudioStreamPlayer _fx = null!, _world = null!, _ui = null!, _bellP = null!, _steps = null!, _river = null!, _mill = null!;
    private readonly Random _rng = new(11);
    private readonly int[] _last = { -1, -1, -1, -1, -1, -1, -1, -1 };
    private double _now, _lastCoin = -1, _beat;
    private Func<(Vector3 centre, float size, float play)>? _view;
    private int _millPlot = -2;
    private Vector3 _millAt;

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Audio";
        Silent = DisplayServer.GetName() == "headless" || Array.IndexOf(OS.GetCmdlineArgs(), "Dummy") >= 0;
        // The buses and their volumes belong to the settings (SettingsApplier); this only makes sure they exist.
        Settings.SettingsApplier.EnsureBuses();
        _cues = SoundCues.Load();
        _coin = Load("sfx/coin-0", 6); _bell = Load("sfx/bell-0", 6);
        _grass = Load("sfx/step-grass-0", 8); _dirt = Load("sfx/step-dirt-0", 8);
        string[] notes = { "d4", "e4", "g4", "a4" };
        _long = new AudioStream[notes.Length];
        for (int i = 0; i < notes.Length; i++) _long[i] = GD.Load<AudioStream>($"res://assets/audio/sfx/bell-long-{notes[i]}.wav");
        _fx = Player("Fx", "Effects", CoinDb, 4);
        _world = Player("World", "Effects", CoinDb, 3);
        _ui = Player("Ui", "Interface", CoinDb, 3);
        _bellP = Player("BellP", "Effects", CoinDb, 3);
        _steps = Player("Steps", "Effects", -16, 3);
        var riverStream = GD.Load<AudioStream>("res://assets/audio/ambience/water-river-loop.ogg");
        if (riverStream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        _river = Player("River", "Ambience", -24, 1);
        _river.Stream = riverStream;
        _mill = Player("Mill", "Ambience", -80, 1);
        if (_cues.TryGetValue("mill", out var m) && m.Streams.Length > 0)
        {
            if (m.Streams[0] is AudioStreamOggVorbis mo) mo.Loop = true;
            _mill.Stream = m.Streams[0];
        }
        Music = new TownMusic();
        AddChild(Music);
        Music.Init(Silent);
        if (!Silent) _river.Play();
        drv.Replaced += () => _millPlot = -2;
    }

    /// <summary>Main gives the view (centre on the map in metres, the camera's size, the size of the normal play view).</summary>
    public void SetView(Func<(Vector3 centre, float size, float play)> view) => _view = view;

    /// <summary>Round 5: a moment's cue by its id in town-sounds.json (a boon, a milestone, the charter drive).</summary>
    public bool Moment(string id) => Cue(id, _ui, ref UiSounds);
    public void UiHover() => Cue("ui-hover", _ui, ref UiSounds);
    public void UiPress() => Cue("ui-press", _ui, ref UiSounds);
    public void UiTab() => Cue("ui-tab", _ui, ref UiSounds);
    public void PageTurn() => Cue("page-turn", _ui, ref BookSounds);
    public void Cannot() => Cue("cannot-afford", _ui, ref Cannots);
    public void BookOpen() => Cue("book-open", _ui, ref BookSounds);
    public void BookClose() => Cue("book-close", _ui, ref BookSounds);

    /// <summary>While the book is closed: the rotation waits and the pause piece plays; on resume the rotation goes on.</summary>
    public void SetPauseMusic(bool on)
    {
        if (PauseMusic == on) return;
        PauseMusic = on;
        Music.SetPaused(on);
    }

    /// <summary>The next age is rung in: the charter bell peal and the rising theme.</summary>
    public void NextAgeRung()
    {
        Charters++;
        Music.PlayRising();
        _peal = 0; _pealAt = _now;
    }

    private int _peal = -1;
    private double _pealAt;

    private AudioStreamPlayer Player(string name, string bus, float db, int poly)
    {
        var p = new AudioStreamPlayer { Name = name, Bus = bus, VolumeDb = db, MaxPolyphony = poly };
        AddChild(p);
        return p;
    }

    private static AudioStream[] Load(string prefix, int n)
    {
        var a = new AudioStream[n];
        for (int i = 0; i < n; i++) a[i] = GD.Load<AudioStream>($"res://assets/audio/{prefix}{i + 1}.wav");
        return a;
    }

    /// <summary>Plays one event's cue unless it played within its least gap. Returns whether it played.</summary>
    private bool Cue(string id, AudioStreamPlayer p, ref int count)
    {
        if (!_cues.TryGetValue(id, out var c) || c.Streams.Length == 0) return false;
        if (_now - c.LastAt < c.MinGap) return false;
        c.LastAt = _now;
        c.Count++;
        count++;
        Played[id] = c.Count;
        if (Silent) return true;
        int k = _rng.Next(c.Streams.Length);
        if (k == c.Last && c.Streams.Length > 1) k = (k + 1) % c.Streams.Length;
        c.Last = k;
        p.Stream = c.Streams[k];
        p.PitchScale = c.PitchVary ? 1f + ((float)_rng.NextDouble() - 0.5f) * 0.06f : 1f;
        p.VolumeDb = CoinDb + c.VolumeDb;
        p.Play();
        return true;
    }

    /// <summary>The Hamlet sets not in town-sounds.json (coin, the bell, steps): a small level spread.</summary>
    private void Play(AudioStreamPlayer p, AudioStream[] set, int slot, float db, ref int count)
    {
        count++;
        if (Silent) return;
        int k = _rng.Next(set.Length);
        if (k == _last[slot] && set.Length > 1) k = (k + 1) % set.Length;
        _last[slot] = k;
        p.Stream = set[k];
        p.PitchScale = 1f + ((float)_rng.NextDouble() - 0.5f) * 0.06f;
        p.VolumeDb = db + ((float)_rng.NextDouble() - 0.5f) * 2f;
        p.Play();
    }

    public override void _Process(double delta)
    {
        _now += delta;
        foreach (var e in _drv.Events)
        {
            switch (e.Kind)
            {
                case TownEventKind.Delivery:
                    // Never a wall of coins at high speed: at most one every 0.25 real seconds.
                    if (_now - _lastCoin >= 0.25) { _lastCoin = _now; Play(_fx, _coin, 0, CoinDb, ref Coins); }
                    break;
                case TownEventKind.Purchase: if (e.Item is not (ShopItemKind.Pave or ShopItemKind.Extend)) Cue("upgrade-bought", _ui, ref Purchases); break;
                case TownEventKind.CottageExtended: Cue("cottage-extended", _world, ref Extends); break;
                case TownEventKind.RequestAsked: Cue("request-asked", _fx, ref Requests); break;
                case TownEventKind.RequestMet: Cue("request-met", _bellP, ref Requests); break;
                case TownEventKind.RequestMissed: Cue("request-missed", _fx, ref Requests); break;
                case TownEventKind.RoadPaved: Cue(e.Surface == Surface.Cobbles ? "road-paved-cobbles" : "road-paved-gravel", _world, ref Paves); break;
                case TownEventKind.LevelReached: if (e.Level > 1) Cue("level-reached", _bellP, ref Levels); break;
                case TownEventKind.BuildingOpened: Cue("gift-arrives", _bellP, ref Levels); break;
                case TownEventKind.AchievementUnlocked: Cue("achievement", _fx, ref Achievements); break;
                case TownEventKind.Charter: Cue("charter-granted", _bellP, ref Charters); break;
                case TownEventKind.BellRung: Play(_bellP, _bell, 3, CoinDb + 2f, ref Bells); break;
            }
        }
        // The peal when the next age is rung in: the four long bells, one every 0.7 s.
        if (_peal >= 0 && _now >= _pealAt)
        {
            if (!Silent) { _bellP.Stream = _long[_peal]; _bellP.PitchScale = 1f; _bellP.VolumeDb = CoinDb + 3f; _bellP.Play(); }
            Bells++;
            _peal++; _pealAt = _now + 0.7;
            if (_peal >= _long.Length) _peal = -1;
        }
        Footsteps(delta);
        MillFrame();
    }

    private void Footsteps(double delta)
    {
        // A hit on a slow beat while anyone walks, grass on new trails, dirt on worn ones.
        _beat += delta;
        if (_beat < 0.42) return;
        _beat = 0;
        var t = _drv.Town;
        int walking = 0;
        for (int i = 0; i < t.VillagerCount; i++) if (t.Villager(i).Phase == VillagerPhase.Walking) walking++;
        if (walking > 0 && !_drv.Paused) Play(_steps, t.RoadsPaved > 0 || t.Level > 3 ? _dirt : _grass, 4, -20 + Math.Min(6, walking), ref Steps);
    }

    /// <summary>The mill's loop: on while the mill works, full within 6 m of the view's centre at the normal play zoom,
    /// gone at 40 m; zooming out to the page and the book takes it down to nothing.</summary>
    private void MillFrame()
    {
        var t = _drv.Town;
        if (_millPlot == -2)
        {
            _millPlot = -1;
            foreach (var p in t.Plan.Plots)
                if (p.Kind == PlotKind.Mill) { _millPlot = p.Id; _millAt = new Vector3((p.X + p.W / 2f) * TownGround.Cell, 0, (p.Y + p.H / 2f) * TownGround.Cell); }
        }
        float level = 0f;
        if (_millPlot >= 0 && t.PlotWorking(_millPlot) && _view != null && !_drv.Paused && !PauseMusic)
        {
            var (centre, size, play) = _view();
            float d = new Vector2(_millAt.X - centre.X, _millAt.Z - centre.Z).Length();
            float near = Math.Clamp(1f - (d - 6f) / 34f, 0f, 1f);
            float zoom = Math.Clamp(1f - (size / Math.Max(1f, play) - 0.6f) / 0.6f, 0f, 1f);
            level = near * near * zoom;
        }
        MillLevel = Mathf.MoveToward(MillLevel, level, 0.05f);
        if (Silent || _mill.Stream == null) return;
        if (MillLevel > 0.001f)
        {
            float db = CoinDb + (_cues.TryGetValue("mill", out var m) ? m.VolumeDb : -6f);
            _mill.VolumeDb = db + Mathf.LinearToDb(MillLevel);
            if (!_mill.Playing) _mill.Play();
        }
        else if (_mill.Playing) _mill.Stop();
    }
}
