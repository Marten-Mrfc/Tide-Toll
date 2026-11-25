using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Basic sound for Prototype 1, after the Sound tab's spec sheet and using only the Hamlet assets:
/// - Buses Master, Music, Ambience, Effects and Interface (made here if the project has none). Sound toggle = Master
///   mute, the volume slider = Master volume. Pause lowers Ambience 6 dB and sweeps a low-pass over Music and
///   Ambience in 300 ms.
/// - River: the loop on Ambience, its level following how much of the river is in view and how close the camera is,
///   panned toward where the river is on screen.
/// - Footsteps: never one sound per villager. On a beat (eighth notes at 100 bpm; quarter notes at 8x, thinned by
///   chance at 2x and 4x) moving walkers are counted per 16 x 16-cell chunk; the busiest chunks on screen get a
///   hit, at most 3 a beat from a pool of 12 voices (the oldest is stolen), placed at the chunk's weighted centre,
///   louder for more walkers, grass or dirt by the ground under them, the ford splash when they wade. Beats 2 and 4
///   are 3 dB lower; every hit varies by +-2 dB and +-50 cents and never repeats the last file. Far out, they fade.
/// - A bell at dawn each in-game day (at most one every 6 real seconds), place / erase knocks for the tools, a muted
///   low knock when a tool is refused, and the theme played quietly now and then with long silences between.
/// - The river (AudioDirector.River.cs): drawbridge winch and creak, boat calls, coins, the ferry's rope, oars and
///   wakes, and the turn-away knock, from the river feed's events.
/// Every cue has a visual twin already on screen: walkers, the river, the new fence or bridge, the date on the
/// chapter page ("Dawn bell"), the ghost and status line. Nothing plays when <see cref="Silent"/> (headless runs and
/// the dummy audio driver): the scheduling still runs and counts cues, so tests and the perf run exercise it.
/// </summary>
public partial class AudioDirector : Node2D
{
    public const int FootVoices = 12, HitsPerBeat = 3, Chunk = 16;
    public const double BeatSec = 0.3; // eighth notes at 100 bpm

    public bool Silent { get; private set; }
    public bool Enabled { get; private set; } = true;
    public float Volume { get; private set; } = 0.8f;
    /// <summary>Cue counts (played, or that would have played when silent).</summary>
    public int Steps, Splashes, Bells, Places, Erases, Refusals, Themes, Beats;
    public float RiverDb { get; private set; } = -80;
    /// <summary>Raised when the dawn bell rings (its visual twin listens).</summary>
    public event Action? Bell;

    private Main _main = null!;
    private AudioStream[] _grass = null!, _dirt = null!, _ford = null!, _bells = null!, _place = null!, _erase = null!;
    private AudioStream _river = null!, _theme = null!;
    private readonly AudioStreamPlayer2D[] _voices = new AudioStreamPlayer2D[FootVoices];
    private readonly double[] _voiceAt = new double[FootVoices];
    private AudioStreamPlayer2D _riverPlayer = null!;
    private AudioStreamPlayer _themePlayer = null!, _bellPlayer = null!, _toolPlayer = null!;
    private readonly int[] _last = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }; // -1: nothing played yet in that slot
    private readonly Random _rng = new(7);
    private double _now, _beatAcc, _riverAcc, _lastBell = -100, _nextTheme = 30, _lowpass = 1;
    private long _beatIndex;
    private int _lastDay = -1;
    private int _master, _music, _ambience, _effects;
    private AudioEffectLowPassFilter? _lpMusic, _lpAmb;
    private readonly int[] _count = new int[64], _dirtN = new int[64], _fordN = new int[64];
    private readonly float[] _sx = new float[64], _sy = new float[64];
    private readonly int[] _pick = new int[HitsPerBeat];

    public void Init(Main main)
    {
        _main = main;
        Name = "Audio";
        Silent = DisplayServer.GetName() == "headless" || Array.IndexOf(OS.GetCmdlineArgs(), "Dummy") >= 0;
        MakeBuses();
        _grass = Load("sfx/step-grass-0", 8); _dirt = Load("sfx/step-dirt-0", 8); _ford = Load("sfx/water-ford-0", 6);
        _bells = Load("sfx/bell-0", 6); _place = Load("sfx/place-0", 6); _erase = Load("sfx/erase-0", 6);
        _river = GD.Load<AudioStream>("res://assets/audio/ambience/water-river-loop.ogg");
        if (_river is AudioStreamOggVorbis ogg) ogg.Loop = true;
        _theme = GD.Load<AudioStream>("res://assets/audio/music/theme-hamlet-draft.ogg");
        for (int i = 0; i < FootVoices; i++)
        {
            _voices[i] = new AudioStreamPlayer2D { Name = $"Step{i}", Bus = "Effects", MaxPolyphony = 1, Attenuation = 0.6f, PanningStrength = 0.8f };
            AddChild(_voices[i]);
        }
        _riverPlayer = new AudioStreamPlayer2D { Name = "River", Bus = "Ambience", Stream = _river, VolumeDb = -80, Attenuation = 0.2f, PanningStrength = 0.5f };
        _themePlayer = new AudioStreamPlayer { Name = "Theme", Bus = "Music", Stream = _theme, VolumeDb = -14 };
        _bellPlayer = new AudioStreamPlayer { Name = "Bell", Bus = "Effects", VolumeDb = -8, MaxPolyphony = 2 };
        _toolPlayer = new AudioStreamPlayer { Name = "Tool", Bus = "Effects", VolumeDb = -6, MaxPolyphony = 3 };
        AddChild(_riverPlayer); AddChild(_themePlayer); AddChild(_bellPlayer); AddChild(_toolPlayer);
        _main.Tools.Placed += _ => Play(_toolPlayer, _place, 3, 0, 1f, ref Places);
        _main.Tools.Erased += () => Play(_toolPlayer, _erase, 4, 0, 1f, ref Erases);
        _main.Tools.Refused += () => Play(_toolPlayer, _place, 5, -12, 0.55f, ref Refusals);
        _main.Sim.TimeChanged += OnTimeChanged;
        _main.Sim.WorldReplaced += OnWorldReplaced;
        InitRiver();
        if (!Silent) _riverPlayer.Play();
        SetVolume(Volume);
    }

    public override void _ExitTree()
    {
        if (_main?.Sim == null) return;
        _main.Sim.TimeChanged -= OnTimeChanged;
        _main.Sim.WorldReplaced -= OnWorldReplaced;
        ExitRiver();
    }

    private void OnWorldReplaced() => _lastDay = -1;

    private static AudioStream[] Load(string prefix, int n)
    {
        var a = new AudioStream[n];
        for (int i = 0; i < n; i++) a[i] = GD.Load<AudioStream>($"res://assets/audio/{prefix}{i + 1}.wav");
        return a;
    }

    private void MakeBuses()
    {
        foreach (var name in new[] { "Music", "Ambience", "Effects", "Interface" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int i = AudioServer.BusCount;
            AudioServer.AddBus(i);
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
        _master = AudioServer.GetBusIndex("Master");
        _music = AudioServer.GetBusIndex("Music");
        _ambience = AudioServer.GetBusIndex("Ambience");
        _effects = AudioServer.GetBusIndex("Effects");
        _lpMusic = LowPass(_music);
        _lpAmb = LowPass(_ambience);
    }

    private static AudioEffectLowPassFilter LowPass(int bus)
    {
        for (int k = 0; k < AudioServer.GetBusEffectCount(bus); k++)
            if (AudioServer.GetBusEffect(bus, k) is AudioEffectLowPassFilter f) return f;
        var lp = new AudioEffectLowPassFilter { CutoffHz = 20000 };
        AudioServer.AddBusEffect(bus, lp);
        return lp;
    }

    public void SetEnabled(bool on)
    {
        Enabled = on;
        AudioServer.SetBusMute(_master, !on);
    }

    public void SetVolume(float v)
    {
        Volume = Mathf.Clamp(v, 0, 1);
        AudioServer.SetBusVolumeDb(_master, Volume <= 0.001f ? -80 : Mathf.LinearToDb(Volume));
    }

    private void OnTimeChanged() => AudioServer.SetBusVolumeDb(_ambience, _main.Sim.Paused ? -6 : 0);

    // ------------------------------------------------------------------ per frame

    public override void _Process(double delta)
    {
        _now += delta;
        var sim = _main.Sim;
        // Pause sweep: the low-pass slides down over 300 ms, and back up on resume.
        double target = sim.Paused ? 0 : 1;
        if (_lowpass != target)
        {
            _lowpass = Mathf.MoveToward((float)_lowpass, (float)target, (float)delta / 0.3f);
            float hz = Mathf.Lerp(900f, 20000f, (float)(_lowpass * _lowpass));
            _lpMusic!.CutoffHz = hz;
            _lpAmb!.CutoffHz = hz;
        }
        DawnBell(sim.World);
        Theme();
        RiverPending();
        _riverAcc += delta;
        if (_riverAcc >= 0.25) { _riverAcc = 0; River(sim.World); }
        if (sim.Paused) return;
        _beatAcc += delta;
        if (_beatAcc < BeatSec) return;
        _beatAcc -= BeatSec;
        if (_beatAcc > BeatSec) _beatAcc = 0; // a long frame: skip, never burst
        _beatIndex++;
        Oars(sim.World);
        int speed = sim.Speed;
        if (speed >= 8 && (_beatIndex & 1) == 1) return;            // quarter notes at top speed
        if (speed is 2 or 4 && _rng.NextDouble() > (speed == 2 ? 0.75 : 0.55)) return; // thinned by chance
        Footsteps(sim.World);
    }

    private void DawnBell(World w)
    {
        int day = (int)Math.Floor(w.Time / w.Params.DaySimSec);
        if (_lastDay < 0 || day < _lastDay) { _lastDay = day; return; }
        if (day == _lastDay) return;
        _lastDay = day;
        if (_now - _lastBell < 6) return;
        _lastBell = _now;
        Play(_bellPlayer, _bells, 6, (float)(_rng.NextDouble() * 2 - 1), 1f, ref Bells);
        Bell?.Invoke();
    }

    private void Theme()
    {
        if (_now < _nextTheme) return;
        // Quiet, then long silences that grow from 2 to 6 minutes over the first half hour (spec: "Rare").
        double gap = 120 + 240 * Math.Min(1, _now / 1800.0);
        _nextTheme = _now + 45 + gap * (0.8 + 0.4 * _rng.NextDouble());
        Themes++;
        if (!Silent) _themePlayer.Play();
    }

    /// <summary>River level: the share of the river's length in view, louder when close; panned to where it is.</summary>
    private void River(World w)
    {
        var rig = _main.Rig;
        var vp = GetViewport().GetVisibleRect().Size;
        if (!rig.PickLocal(Vector2.Zero, out var a) || !rig.PickLocal(vp, out var b)) return;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width, y0 = Math.Max(0, (int)(a.Z / cell)), y1 = Math.Min(n - 1, (int)(b.Z / cell));
        int inView = 0;
        float sumX = 0;
        var rx = w.RiverX;
        for (int y = y0; y <= y1; y++)
        {
            float x = rx[y] * cell;
            if (x >= a.X && x <= b.X) { inView++; sumX += x; }
        }
        float f = inView / (float)n;
        float close = Mathf.Clamp((420f - rig.Camera.Size) / 330f, 0.25f, 1f);
        float gain = Mathf.Sqrt(f) * close;
        float db = gain < 0.01f ? -80 : Mathf.LinearToDb(gain) - 4;
        RiverDb = Mathf.MoveToward(RiverDb, db, 6f);
        _riverPlayer.VolumeDb = RiverDb;
        if (inView > 0)
            _riverPlayer.Position = rig.LocalToScreen(new Vector3(sumX / inView, 0, (y0 + y1) * cell / 2f));
    }

    private void Footsteps(World w)
    {
        Beats++;
        var rig = _main.Rig;
        float size = rig.Camera.Size;
        float zoom = Mathf.Clamp((380f - size) / 250f, 0f, 1f);  // full at play zoom and closer, gone at the whole book
        if (zoom <= 0.02f) return;
        Array.Clear(_count); Array.Clear(_dirtN); Array.Clear(_fordN); Array.Clear(_sx); Array.Clear(_sy);
        int n = w.Grid.Width, per = n / Chunk;
        var wear = w.Wear;
        var ford = w.Ford;
        foreach (ref readonly var k in w.Walkers)
        {
            if (!k.Visible || (k.X == k.Px && k.Y == k.Py)) continue;
            int cx = Math.Clamp((int)k.X, 0, n - 1), cy = Math.Clamp((int)k.Y, 0, n - 1);
            int ch = (cy / Chunk) * per + cx / Chunk;
            _count[ch]++;
            _sx[ch] += (float)k.X; _sy[ch] += (float)k.Y;
            int cell = cy * n + cx;
            if (wear[cell] >= 25) _dirtN[ch]++;
            if (ford[cell] != 0) _fordN[ch]++;
        }
        // The busiest chunks whose centre is on screen.
        var vp = GetViewport().GetVisibleRect().Size;
        float cellM = (float)w.Params.CellMeters;
        int picked = 0;
        for (int i = 0; i < HitsPerBeat; i++) _pick[i] = -1;
        for (int ch = 0; ch < per * per; ch++)
        {
            if (_count[ch] == 0) continue;
            var sp = rig.LocalToScreen(new Vector3(_sx[ch] / _count[ch] * cellM, 0, _sy[ch] / _count[ch] * cellM));
            if (sp.X < 0 || sp.Y < 0 || sp.X > vp.X || sp.Y > vp.Y) continue;
            // Insert into the small top list.
            int at = -1;
            for (int i = 0; i < HitsPerBeat; i++) if (_pick[i] < 0 || _count[_pick[i]] < _count[ch]) { at = i; break; }
            if (at < 0) continue;
            for (int i = HitsPerBeat - 1; i > at; i--) _pick[i] = _pick[i - 1];
            _pick[at] = ch;
            if (picked < HitsPerBeat) picked++;
        }
        bool weak = (_beatIndex & 3) >= 2; // beats 2 and 4 of each pair of quarter notes sit lower
        for (int i = 0; i < picked; i++)
        {
            int ch = _pick[i];
            int c = _count[ch];
            bool splash = _fordN[ch] > 0 && (_beatIndex & 3) == 0;
            var set = splash ? _ford : _dirtN[ch] * 2 >= c ? _dirt : _grass;
            float db = -18 + 3.5f * MathF.Log2(c) + Mathf.LinearToDb(zoom) - (weak ? 3 : 0) + (float)(_rng.NextDouble() * 4 - 2) - i * 2;
            var v = Voice();
            v.Position = rig.LocalToScreen(new Vector3(_sx[ch] / c * cellM, 0, _sy[ch] / c * cellM));
            v.MaxDistance = vp.X * 1.2f;
            int slot = splash ? 2 : set == _dirt ? 1 : 0;
            if (splash) Play2D(v, set, slot, Math.Min(-6, db), ref Splashes);
            else Play2D(v, set, slot, Math.Min(-4, db), ref Steps);
        }
    }

    /// <summary>A free footstep voice, or the oldest one (stolen).</summary>
    private AudioStreamPlayer2D Voice()
    {
        int best = 0;
        for (int i = 0; i < FootVoices; i++)
        {
            if (!_voices[i].Playing) { best = i; break; }
            if (_voiceAt[i] < _voiceAt[best]) best = i;
        }
        _voiceAt[best] = _now;
        return _voices[best];
    }

    private int Next(AudioStream[] set, int slot)
    {
        int last = _last[slot];
        // The first pick may be any file; later picks skip the last one (never the same file twice in a row).
        int k = last < 0 || set.Length < 2 ? _rng.Next(set.Length) : _rng.Next(set.Length - 1);
        if (last >= 0 && set.Length >= 2 && k >= last) k++;
        _last[slot] = k;
        return k;
    }

    private void Play2D(AudioStreamPlayer2D p, AudioStream[] set, int slot, float db, ref int counter)
    {
        counter++;
        if (Silent || !Enabled) return;
        p.Stream = set[Next(set, slot)];
        p.VolumeDb = db;
        p.PitchScale = Cents((float)(_rng.NextDouble() * 100 - 50));
        p.Play();
    }

    private void Play(AudioStreamPlayer p, AudioStream[] set, int slot, float db, float pitch, ref int counter)
    {
        counter++;
        if (Silent || !Enabled) return;
        p.Stream = set[Next(set, slot)];
        p.VolumeDb = db + (p == _toolPlayer ? -6 : -8);
        p.PitchScale = pitch * Cents((float)(_rng.NextDouble() * 60 - 30));
        p.Play();
    }

    private static float Cents(float c) => MathF.Pow(2f, c / 1200f);
}
