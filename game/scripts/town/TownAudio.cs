using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The town's sound, from the existing Hamlet files only (the old AudioDirector served the two chapters and is
/// retired with them; this keeps its buses and its rules: never repeat the last file, small pitch and level spread,
/// nothing plays when headless or with the Dummy driver, but cues are still counted for tests).
/// Coin at a delivery (at most a few a second), a purchase knock, a long bell for a new level and a gift, the bell
/// when rung, footsteps on a slow beat from the walking villagers, the river loop and the theme now and then.
/// There are no dedicated purchase or level sounds in the assets: the purchase uses place-0N.wav and a level uses
/// bell-long-*.wav (listed as missing in docs/plans/game-core-progress.md).
/// </summary>
public partial class TownAudio : Node
{
    public bool Silent { get; private set; }
    public int Coins, Purchases, Levels, Bells, Steps, Themes;

    private TownDriver _drv = null!;
    private AudioStream[] _coin = null!, _place = null!, _bell = null!, _long = null!, _grass = null!, _dirt = null!;
    private AudioStreamPlayer _fx = null!, _ui = null!, _bellP = null!, _steps = null!, _theme = null!, _river = null!;
    private readonly Random _rng = new(11);
    private readonly int[] _last = { -1, -1, -1, -1, -1, -1 };
    private double _now, _lastCoin = -1, _beat, _nextTheme = 8;

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Audio";
        Silent = DisplayServer.GetName() == "headless" || Array.IndexOf(OS.GetCmdlineArgs(), "Dummy") >= 0;
        foreach (var name in new[] { "Music", "Ambience", "Effects", "Interface" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int i = AudioServer.BusCount;
            AudioServer.AddBus(i);
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
        _coin = Load("sfx/coin-0", 6); _place = Load("sfx/place-0", 6); _bell = Load("sfx/bell-0", 6);
        _grass = Load("sfx/step-grass-0", 8); _dirt = Load("sfx/step-dirt-0", 8);
        string[] notes = { "a4", "d4", "e4", "g4" };
        _long = new AudioStream[notes.Length];
        for (int i = 0; i < notes.Length; i++) _long[i] = GD.Load<AudioStream>($"res://assets/audio/sfx/bell-long-{notes[i]}.wav");
        _fx = Player("Fx", "Effects", -10, 4);
        _ui = Player("Ui", "Interface", -6, 2);
        _bellP = Player("BellP", "Effects", -6, 2);
        _steps = Player("Steps", "Effects", -16, 3);
        var themeStream = GD.Load<AudioStream>("res://assets/audio/music/theme-hamlet-draft.ogg");
        _theme = Player("Theme", "Music", -14, 1);
        _theme.Stream = themeStream;
        var riverStream = GD.Load<AudioStream>("res://assets/audio/ambience/water-river-loop.ogg");
        if (riverStream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        _river = Player("River", "Ambience", -24, 1);
        _river.Stream = riverStream;
        if (!Silent) _river.Play();
    }

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

    private void Play(AudioStreamPlayer p, AudioStream[] set, int slot, float db, ref int count)
    {
        count++;
        if (Silent) return;
        int k = _rng.Next(set.Length);
        if (k == _last[slot] && set.Length > 1) k = (k + 1) % set.Length;
        _last[slot] = k;
        p.Stream = set[k];
        p.PitchScale = 1f + ((float)_rng.NextDouble() - 0.5f) * 0.06f;
        p.VolumeDb = db + ((float)_rng.NextDouble() - 0.5f) * 4f;
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
                    if (_now - _lastCoin >= 0.25) { _lastCoin = _now; Play(_fx, _coin, 0, -12, ref Coins); }
                    break;
                case TownEventKind.Purchase: Play(_ui, _place, 1, -6, ref Purchases); break;
                case TownEventKind.LevelReached: if (e.Level > 1) Play(_bellP, _long, 2, -8, ref Levels); break;
                case TownEventKind.BuildingOpened: Play(_bellP, _long, 2, -10, ref Levels); break;
                case TownEventKind.BellRung: Play(_bellP, _bell, 3, -4, ref Bells); break;
            }
        }
        // Footsteps: a hit on a slow beat while anyone walks, grass on new trails, dirt on worn ones.
        _beat += delta;
        if (_beat >= 0.42)
        {
            _beat = 0;
            var t = _drv.Town;
            int walking = 0;
            for (int i = 0; i < t.VillagerCount; i++) if (t.Villager(i).Phase == VillagerPhase.Walking) walking++;
            if (walking > 0 && !_drv.Paused) Play(_steps, t.RoadsPaved > 0 || t.Level > 3 ? _dirt : _grass, 4, -20 + Math.Min(6, walking), ref Steps);
        }
        // The theme now and then, with long silences between.
        if (_now >= _nextTheme)
        {
            _nextTheme = _now + 150 + _rng.Next(60);
            Themes++;
            if (!Silent) _theme.Play();
        }
    }
}
