using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 4: the river's sounds. Events in assets/audio/sfx/river-sounds.json (boat-moored, boat-cast-off,
/// cargo-on-quay, tide-turned, loom, sheep, crane, toll-paid, seal-granted) play their own files at the coin's gain;
/// the river sounds that have files but no table row (boat-call, oar-splash, ferry-rope, drawbridge-raise,
/// drawbridge-lower, water-ford) load by file name. Boat moored and cast off come from the boats' phase changes,
/// the tide's turn from TideRising flipping, oar splashes while a rowboat moves, the ford's splash while people wade,
/// the loom and the sheep now and then while the weaver and the pasture work. Counted in <see cref="Played"/> even
/// when silent (tests).
/// </summary>
public partial class RiverAudio : Node
{
    public const string JsonPath = "res://assets/audio/sfx/river-sounds.json";
    public readonly Dictionary<string, int> Played = new();

    private TownDriver _drv = null!;
    private readonly Dictionary<string, (AudioStream[] s, float db, bool vary)> _cues = new();
    private readonly Dictionary<string, double> _last = new();
    private AudioStreamPlayer _p = null!;
    private bool _silent;
    private readonly Random _rng = new(23);
    private BoatPhase[] _phase = Array.Empty<BoatPhase>();
    private bool _rising, _seen;
    private double _now, _oar, _wade, _loom, _sheep;

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "RiverAudio";
        _silent = DisplayServer.GetName() == "headless" || Array.IndexOf(OS.GetCmdlineArgs(), "Dummy") >= 0;
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
                        string path = "res://" + f.AsString();
                        if (ResourceLoader.Exists(path)) files.Add(GD.Load<AudioStream>(path));
                    }
                    if (files.Count > 0)
                        _cues[k.AsString()] = (files.ToArray(), (float)e["volume_db"].AsDouble(), e.TryGetValue("pitch_vary", out var pv) && pv.AsBool());
                }
        }
        foreach (var (id, prefix, n, db) in new (string, string, int, float)[]
                 {
                     ("boat-call", "boat-call-0", 6, 0f), ("oar-splash", "oar-splash-0", 6, -6f), ("ferry-rope", "ferry-rope-0", 4, -2f),
                     ("drawbridge-raise", "drawbridge-raise-0", 3, 1f), ("drawbridge-lower", "drawbridge-lower-0", 3, 1f),
                     ("water-ford", "water-ford-0", 6, -4f), ("seal-granted", "seal-0", 1, 4f), ("toll-paid", "toll-0", 3, 0f),
                 })
        {
            if (_cues.ContainsKey(id)) continue;
            var files = new List<AudioStream>();
            for (int i = 1; i <= n; i++)
            {
                string path = $"res://assets/audio/sfx/{prefix}{i}.wav";
                if (ResourceLoader.Exists(path)) files.Add(GD.Load<AudioStream>(path));
            }
            if (files.Count > 0) _cues[id] = (files.ToArray(), db, true);
        }
        _p = new AudioStreamPlayer { Name = "RiverFx", Bus = "Effects", VolumeDb = TownAudio.CoinDb, MaxPolyphony = 4 };
        AddChild(_p);
        drv.Replaced += () => { _sealDue = 0; _seen = false; _phase = Array.Empty<BoatPhase>(); };
    }

    /// <summary>Seconds after the Seal event at which its sound plays anyway if no banner asked for it.</summary>
    public const double SealWait = 8;
    private double _sealDue;

    /// <summary>A large message's sound that lives in the river table (the seal): plays now, and the Seal event's
    /// own wait is over. False when it is not a river cue.</summary>
    public bool Large(string id)
    {
        if (!_cues.ContainsKey(id)) return false;
        if (id == "seal-granted") _sealDue = 0;
        return Cue(id, 2);
    }

    /// <summary>Plays a cue unless it played within the gap; counted either way it plays.</summary>
    public bool Cue(string id, double gap = 0.25)
    {
        if (!_cues.TryGetValue(id, out var c)) return false;
        if (_last.TryGetValue(id, out double at) && _now - at < gap) return false;
        _last[id] = _now;
        Played[id] = Played.TryGetValue(id, out int n) ? n + 1 : 1;
        if (_silent) return true;
        if (_p.Playing && _p.GetStreamPlayback() is AudioStreamPlaybackPolyphonic poly)
        {
            poly.PlayStream(c.s[_rng.Next(c.s.Length)], 0, c.db, c.vary ? 0.94f + (float)_rng.NextDouble() * 0.12f : 1f);
            return true;
        }
        _p.Stream = new AudioStreamPolyphonic { Polyphony = 6 };
        _p.Play();
        if (_p.GetStreamPlayback() is AudioStreamPlaybackPolyphonic p2) p2.PlayStream(c.s[_rng.Next(c.s.Length)], 0, c.db, 1f);
        return true;
    }

    public override void _Process(double delta)
    {
        _now += delta;
        var t = _drv.Town;
        foreach (var e in _drv.Events)
        {
            switch (e.Kind)
            {
                case TownEventKind.BoatArrived: Cue("boat-call", 1.0); break;
                case TownEventKind.LandingDelivery: Cue("cargo-on-quay", 0.3); break;
                case TownEventKind.TollPaid: Cue("toll-paid"); break;
                case TownEventKind.FerryDocked: Cue("ferry-rope", 0.5); break;
                case TownEventKind.DrawbridgeRaised: Cue("drawbridge-raise", 0.5); break;
                case TownEventKind.DrawbridgeLowered: Cue("drawbridge-lower", 0.5); break;
                // Night 3: the seal sounds when its banner "The borough seal" appears (TownHud passes "seal-granted"
                // to TownAudio.Large, which hands it here). Should no banner ask within SealWait, it plays then.
                case TownEventKind.Seal: _sealDue = _now + SealWait; break;
                case TownEventKind.Purchase when e.Item == ShopItemKind.Crane: Cue("crane"); break;
            }
        }
        if (_sealDue > 0 && _now >= _sealDue) { _sealDue = 0; Cue("seal-granted", 2); }
        if (!t.InRiverAge) return;
        // Boats moored and cast off: their phase changes.
        int n = t.BoatCount;
        if (_phase.Length != n) { _phase = new BoatPhase[n]; for (int i = 0; i < n; i++) _phase[i] = t.Boat(i).Phase; }
        bool rowing = false;
        for (int i = 0; i < n; i++)
        {
            var b = t.Boat(i);
            if (b.Phase != _phase[i])
            {
                if (b.Phase == BoatPhase.Moored) Cue("boat-moored", 0.4);
                else if (b.Phase == BoatPhase.Leaving && _phase[i] == BoatPhase.Moored) Cue("boat-cast-off", 0.4);
                _phase[i] = b.Phase;
            }
            if (b.Kind == BoatKind.Rowboat && b.Phase is BoatPhase.Arriving or BoatPhase.Leaving) rowing = true;
        }
        // The tide turns.
        if (_seen && t.TideRising != _rising) Cue("tide-turned", 2);
        _rising = t.TideRising; _seen = true;
        if (rowing && (_oar += delta) > 1.6) { _oar = 0; Cue("oar-splash", 0.8); }
        bool wading = t.CrossingExists(RiverLinkKind.Ford) && t.FordOpen && t.Crossings(RiverLinkKind.Ford) > 0 && WadingNow(t);
        if (wading && (_wade += delta) > 2.2) { _wade = 0; Cue("water-ford", 1.0); }
        if (t.RiverPlotGiven(1) && (_loom += delta) > 9) { _loom = 0; Cue("loom", 4); }
        if (t.RiverPlotGiven(4) && (_sheep += delta) > 14) { _sheep = 0; Cue("sheep", 6); }
    }

    /// <summary>Someone stands in the water at the ford (between its two banks) now.</summary>
    private static bool WadingNow(Town t)
    {
        var rp = t.RiverPlan!;
        int f = rp.LinkOf(RiverLinkKind.Ford);
        if (f < 0) return false;
        var l = rp.Links[f];
        double x0 = Math.Min(l.Px[0], l.Px[^1]) + 1, x1 = Math.Max(l.Px[0], l.Px[^1]) - 1, row = l.Py[0];
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            if (v.Road < 0 && Math.Abs(v.Y - row) < 1 && v.X > x0 && v.X < x1) return true;
        }
        return false;
    }
}
