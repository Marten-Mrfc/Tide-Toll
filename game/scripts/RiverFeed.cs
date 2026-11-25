using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The river's events, drained from the simulation once a frame (right after the ticks) and handed on: the raw event
/// to listeners (<see cref="Event"/>: sound, the marks on the water, the floating notes), and the notable ones written
/// as margin notes for the chapter page's chronicle ("Day 3: A barge turned back at the footbridge: its mast is too
/// tall"). It remembers, per boat, its kind and where it last waited, so a turn-back note can say why; and per
/// drawbridge, how many boats went through and how many walkers waited in each raise, for the float after the cycle
/// ("+2 boats through; 9 walkers waited"). Strings are built only when a notable event happens, never per frame.
/// </summary>
public partial class RiverFeed : Node
{
    public const int ChronicleLines = 4;
    private const int Memo = 64;

    private Main _main = null!;
    private readonly List<RiverEvent> _events = new(256);
    private readonly string[] _notes = new string[ChronicleLines];
    private int _noteCount;
    private readonly int[] _boatId = new int[Memo], _boatAt = new int[Memo];
    private readonly byte[] _boatKind = new byte[Memo];
    // Per drawbridge (by crossing id % 16): boats through and the largest walker queue in the current raise.
    private readonly int[] _cycleBoats = new int[16], _cycleQueue = new int[16];

    /// <summary>Every river event, as it is drained (once a frame, oldest first).</summary>
    public event Action<RiverEvent>? Event;
    /// <summary>A short note for a float on the map at a crossing (bridge cycles): crossing id and text.</summary>
    public event Action<int, string>? CycleNote;
    /// <summary>The chronicle changed (a note was added); read <see cref="Note"/>.</summary>
    public event Action? Chronicle;
    /// <summary>Events drained so far, and notes written (tests).</summary>
    public int Drained { get; private set; }
    public int NotesWritten { get; private set; }
    public int NoteCount => _noteCount;
    /// <summary>The day the last spring flood rose, and the day the river last froze (1-based; 0 none yet).</summary>
    public int FloodDay { get; private set; }
    public int FrozenDay { get; private set; }

    private int Day()
    {
        var w = _main.Sim.World;
        return (int)Math.Floor(w.Time / w.Params.DaySimSec + 1e-9) + 1;
    }

    /// <summary>The i-th newest chronicle note (0 = newest).</summary>
    public string Note(int i) => i < _noteCount ? _notes[i] : "";

    public void Init(Main main)
    {
        _main = main;
        Name = "RiverFeed";
        ProcessPriority = -90; // after the driver's ticks, before the views and the interface
        Array.Fill(_boatId, -1);
        main.Sim.WorldReplaced += OnWorldReplaced;
    }

    public override void _ExitTree()
    {
        if (_main?.Sim != null) _main.Sim.WorldReplaced -= OnWorldReplaced;
    }

    private void OnWorldReplaced()
    {
        _noteCount = 0;
        FloodDay = FrozenDay = 0;
        Array.Fill(_boatId, -1);
        Array.Clear(_cycleBoats); Array.Clear(_cycleQueue);
        Chronicle?.Invoke();
    }

    public override void _Process(double delta) => Drain();

    /// <summary>Drains and dispatches the river's events now (also called by tests after stepping ticks).</summary>
    public void Drain()
    {
        var r = _main.Sim.World.River;
        if (r == null) return;
        // Walkers waiting at each drawbridge, for the cycle note: the largest queue seen during the raise.
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            if (x.Type != CrossingType.Drawbridge || x.State == BridgeState.Closed) continue;
            int k = x.Id & 15;
            _cycleQueue[k] = Math.Max(_cycleQueue[k], x.QueueWest + x.QueueEast);
        }
        _events.Clear();
        r.DrainEvents(_events);
        for (int i = 0; i < _events.Count; i++)
        {
            var e = _events[i];
            Drained++;
            Remember(e);
            Event?.Invoke(e);
        }
    }

    private void Remember(in RiverEvent e)
    {
        int m = e.Boat >= 0 ? e.Boat % Memo : 0;
        switch (e.Kind)
        {
            case RiverEventKind.BoatArrived:
                _boatId[m] = e.Boat; _boatKind[m] = (byte)e.Value; _boatAt[m] = -1;
                break;
            case RiverEventKind.BoatWaiting:
                if (_boatId[m] == e.Boat)
                {
                    bool first = _boatAt[m] != e.Crossing;
                    _boatAt[m] = e.Crossing;
                    var x = Find(e.Crossing);
                    if (first && x != null && x.Type == CrossingType.Footbridge && _boatKind[m] == (byte)BoatKind.Barge)
                        Write($"A barge waits below the footbridge: its mast is too tall to pass");
                }
                break;
            case RiverEventKind.BoatTurnedBack:
                Write($"A {Kind(m, e.Boat)} turned back {Why(m, e.Boat)}");
                break;
            case RiverEventKind.BoatSold:
                if (e.Value > 0) Write($"A {Kind(m, e.Boat)} bought grain at the landing: +{Coin(e.Value)}");
                else Write($"A {Kind(m, e.Boat)} left the landing empty: no grain came down in time");
                break;
            case RiverEventKind.BoatPassed:
                _cycleBoats[e.Crossing & 15]++;
                break;
            case RiverEventKind.BridgeRaising:
            {
                var x = Find(e.Crossing);
                bool hand = _main.Sim.LastLeverTick == e.Tick || x == null || !x.Keeper;
                Write(hand ? "You raised the drawbridge" : "The keeper raised the bridge for a barge");
                _cycleBoats[e.Crossing & 15] = 0;
                _cycleQueue[e.Crossing & 15] = 0;
                break;
            }
            case RiverEventKind.BridgeLowered:
            {
                int k = e.Crossing & 15, boats = _cycleBoats[k], q = _cycleQueue[k];
                string b = boats == 1 ? "+1 boat through" : $"+{boats} boats through";
                CycleNote?.Invoke(e.Crossing, q > 0 ? $"{b}; {q} walker{(q == 1 ? "" : "s")} waited" : b);
                _cycleBoats[k] = 0; _cycleQueue[k] = 0;
                break;
            }
            case RiverEventKind.KeeperHired:
                Write($"A keeper was hired for the drawbridge ({Coin(e.Value)})");
                break;
            case RiverEventKind.CrossingBuilt:
            {
                var x = Find(e.Crossing);
                if (x != null) Write($"A {World.CrossingName(x.Type)} was built for {Coin(e.Value)}");
                break;
            }
            case RiverEventKind.CrossingRemoved:
                Write($"A crossing was taken down ({Coin(e.Value)})");
                break;
            case RiverEventKind.SeasonSettled:
                Write(e.Value >= 0 ? $"The season's books: +{Coin(e.Value)} of hearth tax after upkeep" : $"The season's books: {Coin(e.Value)} after upkeep and wages");
                break;
            case RiverEventKind.CannotAfford:
                Write($"Not enough coin: that costs {Coin(e.Value)}");
                break;
            case RiverEventKind.FloodRising:
                FloodDay = Day();
                Write(e.Value >= 2 ? $"Spring flood: the river is over its banks for {e.Value:0} days and the ford is closed"
                                   : "Spring flood: the river is over its banks for a day and the ford is closed");
                break;
            case RiverEventKind.FloodReceded:
                Write("The flood went down: the ford is open again, and the banks are mud until summer");
                break;
            case RiverEventKind.FloodRepairs:
                Write($"Flood repairs to the houses by the river: -{Coin(e.Value)}");
                break;
            case RiverEventKind.RiverFrozen:
                FrozenDay = Day();
                Write("The river froze: boats are held in the ice, and villagers cross anywhere");
                break;
            case RiverEventKind.Thaw:
                Write("The ice broke up: the paths across it are gone and boats move again");
                break;
        }
    }

    private string Kind(int m, int boat) => _boatId[m] == boat && _boatKind[m] == (byte)BoatKind.Rowboat ? "rowboat" : "barge";

    private string Why(int m, int boat)
    {
        if (_boatId[m] != boat) return "downriver";
        int at = _boatAt[m];
        if (at == -2) return "below the landing: both berths were taken";
        if (at == -3) return "stuck behind other boats";
        var x = Find(at);
        if (x == null) return "downriver";
        return x.Type switch
        {
            CrossingType.Footbridge => "at the footbridge: its mast is too tall",
            CrossingType.Drawbridge => "at the drawbridge: it was not raised in time",
            _ => "downriver",
        };
    }

    private Crossing? Find(int id)
    {
        var r = _main.Sim.World.River;
        if (r == null || id < 0) return null;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i];
        return null;
    }

    private void Write(string text)
    {
        int day = Day();
        for (int i = ChronicleLines - 1; i > 0; i--) _notes[i] = _notes[i - 1];
        _notes[0] = $"Day {day}: {text}.";
        _noteCount = Math.Min(ChronicleLines, _noteCount + 1);
        NotesWritten++;
        Chronicle?.Invoke();
    }

    public static string Coin(double c) => (c < 0 ? "-" : "") + Math.Floor(Math.Abs(c) + 1e-9).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "c";
}
