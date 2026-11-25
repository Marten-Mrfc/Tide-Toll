using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The river's sounds (Sound tab, river traffic), each with its visual twin, from the river feed's events:
/// - Drawbridge raise and lower (winch ratchet, creak, thud) on Effects at the bridge: a hand on the lever sounds at
///   once; the keeper's raises wait for the next bar line (1.2 s). Twin: the leaf moving, the signal arm, the badge.
/// - Boat calls (hand bell for rowboats, horn for barges) only when a boat is held or arrives at the landing, at most
///   one every 2 seconds. Twin: the patience ring.
/// - Coins (Interface bus) when a boat pays. Twin: the yellow coin rising at the landing.
/// - The ferry's rope when the raft sets off with riders, at most one every 1.5 s. Twin: the raft and its dotted line.
/// - Oars on beats 1 and 3 for the nearest rowboat under way on screen, and a wake wash now and then behind a barge.
///   Twin: the boat and its wake.
/// - The turn-away knock when a boat gives up. Twin: the vermillion diamond and "Turned back".
/// Voices: 4 for boats (oars, calls, rope, turn-away; the oldest is stolen) and 2 for the bridge, on top of the
/// 12 footstep voices: within the Sound tab's desktop budget for boats (12) and bells and horns (6).
/// </summary>
public partial class AudioDirector
{
    public const int BoatVoices = 4, BridgeVoices = 2;
    /// <summary>River cue counts (played, or that would have played when silent).</summary>
    public int BridgeCues, Calls, CoinCues, Ropes, OarCues, TurnAways;

    private AudioStream[] _raise = null!, _lower = null!, _calls = null!, _coins = null!, _rope = null!, _oars = null!, _turn = null!;
    private readonly AudioStreamPlayer2D[] _boatV = new AudioStreamPlayer2D[BoatVoices], _bridgeV = new AudioStreamPlayer2D[BridgeVoices];
    private readonly double[] _boatAt = new double[BoatVoices];
    private AudioStreamPlayer _coinPlayer = null!;
    private double _lastCall = -100, _lastRope = -100, _pendingAt = -1;
    private int _pendingBridge = -1, _bridgeNext;
    private bool _pendingRaise;

    private void InitRiver()
    {
        _raise = LoadRiver("drawbridge-raise-0", 3); _lower = LoadRiver("drawbridge-lower-0", 3);
        _calls = LoadRiver("boat-call-0", 6); _coins = LoadRiver("coin-0", 6); _rope = LoadRiver("ferry-rope-0", 4);
        _oars = LoadRiver("oar-splash-0", 6); _turn = LoadRiver("boat-turn-away-0", 3);
        for (int i = 0; i < BoatVoices; i++)
        {
            _boatV[i] = new AudioStreamPlayer2D { Name = $"Boat{i}", Bus = "Effects", MaxPolyphony = 1, Attenuation = 0.6f, PanningStrength = 0.8f };
            AddChild(_boatV[i]);
        }
        for (int i = 0; i < BridgeVoices; i++)
        {
            _bridgeV[i] = new AudioStreamPlayer2D { Name = $"Bridge{i}", Bus = "Effects", MaxPolyphony = 1, Attenuation = 0.4f, PanningStrength = 0.7f };
            AddChild(_bridgeV[i]);
        }
        _coinPlayer = new AudioStreamPlayer { Name = "Coin", Bus = "Interface", MaxPolyphony = 2 };
        AddChild(_coinPlayer);
        _main.Feed.Event += OnRiverEvent;
    }

    private void ExitRiver()
    {
        if (_main?.Feed != null) _main.Feed.Event -= OnRiverEvent;
    }

    private static AudioStream[] LoadRiver(string prefix, int n) => Load("sfx/" + prefix, n);

    private void OnRiverEvent(RiverEvent e)
    {
        switch (e.Kind)
        {
            case RiverEventKind.BridgeRaising:
            case RiverEventKind.BridgeLowering:
            {
                bool raise = e.Kind == RiverEventKind.BridgeRaising;
                if (_main.Sim.LastLeverTick == e.Tick) BridgeSound(e.Crossing, raise);
                else
                {
                    // The keeper (or the bridge lowering itself) snaps to the next bar line.
                    double bar = BeatSec * 4;
                    _pendingAt = Math.Ceiling(_now / bar + 1e-6) * bar;
                    _pendingBridge = e.Crossing;
                    _pendingRaise = raise;
                }
                break;
            }
            case RiverEventKind.BoatWaiting:
            case RiverEventKind.BoatMoored:
                if (_now - _lastCall < 2) break;
                if (!BoatScreen(e.Boat, out var at, out bool barge)) break;
                _lastCall = _now;
                // Bells 1 to 3 for rowboats, horns 4 to 6 for barges.
                PlayBoat(at, _calls, barge ? 3 : 0, 3, 8, -10, ref Calls);
                break;
            case RiverEventKind.BoatSold when e.Value > 0:
                Play(_coinPlayer, _coins, 9, Math.Min(0, -6 + (float)Math.Log2(e.Value / 8.0 + 1) * 2), 1f, ref CoinCues);
                break;
            case RiverEventKind.FerryDeparted when e.Value > 0:
                if (_now - _lastRope < 1.5) break;
                _lastRope = _now;
                if (CrossingScreen(e.Crossing, out var fat)) PlayBoat(fat, _rope, 0, 4, 10, -12, ref Ropes);
                break;
            case RiverEventKind.BoatTurnedBack:
                if (BoatScreen(e.Boat, out var tat, out _)) PlayBoat(tat, _turn, 0, 3, 11, -8, ref TurnAways);
                else TurnAways++;
                break;
        }
    }

    private void RiverPending()
    {
        if (_pendingAt < 0 || _now < _pendingAt) return;
        _pendingAt = -1;
        BridgeSound(_pendingBridge, _pendingRaise);
    }

    private void BridgeSound(int crossing, bool raise)
    {
        BridgeCues++;
        if (Silent || !Enabled || !CrossingScreen(crossing, out var at)) return;
        var v = _bridgeV[_bridgeNext];
        _bridgeNext = (_bridgeNext + 1) % BridgeVoices;
        v.Position = at;
        v.MaxDistance = GetViewport().GetVisibleRect().Size.X * 1.5f;
        var set = raise ? _raise : _lower;
        v.Stream = set[Next(set, raise ? 12 : 13)];
        v.VolumeDb = -8;
        v.PitchScale = Cents((float)(_rng.NextDouble() * 40 - 20));
        v.Play();
    }

    /// <summary>Oars on beats 1 and 3 for the nearest rowboat under way on screen; a barge's wake wash now and then.</summary>
    private void Oars(World w)
    {
        var r = w.River;
        if (r == null || (_beatIndex & 1) == 1) return;
        var rig = _main.Rig;
        if (rig.Camera.Size > 300f) return; // far out, boats are silent
        var vp = GetViewport().GetVisibleRect().Size;
        var centre = vp / 2f;
        var boats = r.Boats;
        int best = -1;
        float bestD = float.MaxValue;
        Vector2 bestAt = default;
        for (int i = 0; i < boats.Length; i++)
        {
            ref readonly Boat b = ref boats[i];
            if (b.Y == b.PrevY) continue;
            if (b.Kind == BoatKind.Barge && _rng.NextDouble() > 0.12) continue; // a wash now and then
            if (!_main.Boats.BoatPosition(b.Id, out var local)) continue;
            var at = rig.LocalToScreen(local);
            if (at.X < 0 || at.Y < 0 || at.X > vp.X || at.Y > vp.Y) continue;
            float d = at.DistanceSquaredTo(centre);
            if (d < bestD) { bestD = d; best = i; bestAt = at; }
        }
        if (best < 0) return;
        bool barge = boats[best].Kind == BoatKind.Barge;
        // Strokes 1 to 4 for oars, washes 5 and 6 behind a barge.
        PlayBoat(bestAt, _oars, barge ? 4 : 0, barge ? 2 : 4, barge ? 14 : 15, (_beatIndex & 3) == 0 ? -14 : -17, ref OarCues);
    }

    private bool BoatScreen(int id, out Vector2 at, out bool barge)
    {
        at = default;
        barge = false;
        var r = _main.Sim.World.River;
        if (r == null) return false;
        var bs = r.Boats;
        for (int i = 0; i < bs.Length; i++) if (bs[i].Id == id) { barge = bs[i].Kind == BoatKind.Barge; break; }
        if (!_main.Boats.BoatPosition(id, out var local)) return false;
        at = _main.Rig.LocalToScreen(local);
        return true;
    }

    private bool CrossingScreen(int id, out Vector2 at)
    {
        at = default;
        var w = _main.Sim.World;
        var r = w.River;
        if (r == null) return false;
        var xs = r.Crossings;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        for (int i = 0; i < xs.Count; i++)
        {
            if (xs[i].Id != id || xs[i].Cells.Count == 0) continue;
            int c = xs[i].Cells[0];
            at = _main.Rig.LocalToScreen(new Vector3((c % n + 0.5f) * cell, 0, (xs[i].Row + 0.5f) * cell));
            return true;
        }
        return false;
    }

    /// <summary>One boat voice (the oldest is stolen) playing a file from set[first .. first + count).</summary>
    private void PlayBoat(Vector2 at, AudioStream[] set, int first, int count, int slot, float db, ref int counter)
    {
        counter++;
        if (Silent || !Enabled) return;
        int best = 0;
        for (int i = 0; i < BoatVoices; i++)
        {
            if (!_boatV[i].Playing) { best = i; break; }
            if (_boatAt[i] < _boatAt[best]) best = i;
        }
        _boatAt[best] = _now;
        var v = _boatV[best];
        v.Position = at;
        v.MaxDistance = GetViewport().GetVisibleRect().Size.X * 1.2f;
        int last = _last[slot];
        int k = first + _rng.Next(count);
        if (count > 1 && k == last) k = first + (k - first + 1) % count;
        _last[slot] = k;
        v.Stream = set[k];
        v.VolumeDb = db + (float)(_rng.NextDouble() * 3 - 1.5);
        v.PitchScale = Cents((float)(_rng.NextDouble() * 60 - 30));
        v.Play();
    }
}
