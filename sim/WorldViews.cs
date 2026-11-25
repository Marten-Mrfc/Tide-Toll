using System;
using System.Collections.Generic;

namespace TideToll.Sim;

// Read-only views for rendering, tests and tools. Spans point at live state: read them, do not keep them
// across ticks. Every change goes through a command (Tick, PlaceFence, PlaceBridge, Erase, SetWalkerCount).
public sealed partial class World
{
    public int Seed { get; }
    public SimParams Params => _p;
    public Grid Grid => _g;
    public MapData Map => _map;
    /// <summary>Sim seconds since the start.</summary>
    public double Time => _time;
    public long Ticks => _ticks;
    /// <summary>Edit counter: the renderer redraws static layers (fences, bridges) when it changes.</summary>
    public int Version => _version;

    /// <summary>Wear W per cell, 0..100.</summary>
    public ReadOnlySpan<float> Wear => _wear;
    /// <summary>Recent footsteps per cell, decaying with FlowHalfLife.</summary>
    public ReadOnlySpan<float> Flow => _flow;
    /// <summary>Routing cost per cell (+Infinity where blocked), as last rebuilt.</summary>
    public ReadOnlySpan<float> Cost => _cost;
    /// <summary>The frugal walkers' routing cost per cell (empty unless SimParams.UseFrugal).</summary>
    public ReadOnlySpan<float> CostX => _costX;
    /// <summary>Gravel per cell (1 where laid).</summary>
    public ReadOnlySpan<byte> Gravel => _gravel;
    /// <summary>Gate per cell (1 on a fence cell walkers may pass).</summary>
    public ReadOnlySpan<byte> Gate => _gate;
    /// <summary>1 on the two cells of a two-cell (stair-corner) gate.</summary>
    public ReadOnlySpan<byte> GatePair => _gatePair;
    /// <summary>The wear and traffic discount lookup tables (for the probes that compare them with the toy's).</summary>
    public ReadOnlySpan<float> WearLookup => _wearLut;
    public ReadOnlySpan<float> FlowLookup => _flowLut;
    /// <summary>Moving average of walkers per cell (crowding).</summary>
    public ReadOnlySpan<float> Occupancy => _occ;
    public ReadOnlySpan<byte> Hollow => _hollow;
    public ReadOnlySpan<byte> Fence => _fence;
    /// <summary>Land cells (not water, not a building footprint): the base for coverage.</summary>
    public ReadOnlySpan<byte> Land => _land;
    /// <summary>Bridge id per cell, -1 where there is none.</summary>
    public ReadOnlySpan<short> BridgeAt => _bridgeAt;
    public ReadOnlySpan<byte> Water => _map.Water;
    public ReadOnlySpan<byte> Ford => _map.Ford;
    public ReadOnlySpan<byte> Crop => _map.Crop;
    public ReadOnlySpan<byte> Footprint => _map.Footprint;
    public ReadOnlySpan<float> RiverX => _map.RiverX;
    /// <summary>Trips out per (house, destination) pair, decaying; index home * Buildings.Length + dest.</summary>
    public ReadOnlySpan<float> TripRate => _tripRate;

    /// <summary>Walkers walking on each cell right now (Out or Back).</summary>
    public ReadOnlySpan<ushort> WalkersOnCell => _here;
    public ReadOnlySpan<Walker> Walkers => new(_walkers, 0, _walkerCount);
    public int WalkerCount => _walkerCount;
    public ReadOnlySpan<Building> Buildings => _map.Buildings;
    /// <summary>Building indices of the houses.</summary>
    public ReadOnlySpan<int> Houses => _byKind[(int)BuildingKind.House];
    /// <summary>Building indices of one kind, in map order.</summary>
    public ReadOnlySpan<int> OfKind(BuildingKind k) => _byKind[(int)k];
    public IReadOnlyList<Bridge> Bridges => _bridges;
    /// <summary>Cost-to-go field towards building t's door.</summary>
    public ReadOnlySpan<float> Field(int t) => _fields[t];
    /// <summary>Cost-to-go field on the frugal view towards building t's door (empty unless SimParams.UseFrugal).</summary>
    public ReadOnlySpan<float> FieldX(int t) => _fieldsX[t];
    public Metrics Metrics => _metrics;

    public Ground GroundAt(int c) => _p.GroundOf(_wear[c], _hollow[c] == 1);

    /// <summary>Raw state of the walker random stream (for saves and the preview isolation test).</summary>
    public uint RngState => _rng.State;

    /// <summary>Recent trips per day from house <paramref name="home"/> to building <paramref name="dest"/>.</summary>
    public double TripRatePerDay(int home, int dest) => _tripRate[home * _map.Buildings.Length + dest] / _p.TripRateDays;

    /// <summary>Walkers drawn on the map (those not inside a building).</summary>
    public int WalkersOut()
    {
        int n = 0;
        for (int i = 0; i < _walkerCount; i++) if (_walkers[i].Visible) n++;
        return n;
    }

    /// <summary>The health metrics and clock. Allocation-free (buffers are reused).</summary>
    public SimStats Stats()
    {
        var h = _metrics.Compute(_time, _wear, _land);
        return h with
        {
            Walkers = _walkerCount,
            SimTime = _time,
            Day = (int)Math.Floor(_time / _p.DaySimSec) + 1,
            Season = (int)Math.Floor(_time / _p.SeasonSimSec) % 4,
            Year = (int)Math.Floor(_time / (_p.SeasonSimSec * 4)) + 1,
            FirstTrailTime = _metrics.FirstTrailTime,
        };
    }

    private FlowPredictor? _predictor;

    /// <summary>
    /// Flow preview for a hypothetical edit, computed in one go with a predictor this world keeps (the toy's
    /// previewFlows). Read-only: it never changes the run. The game can instead own a <see cref="FlowPredictor"/>
    /// and spread the work over frames with Begin and Step.
    /// </summary>
    public FlowPreview PreviewFlows(in WhatIf change, int maxPairs = 5)
    {
        _predictor ??= new FlowPredictor(this);
        return _predictor.Compute(change, maxPairs);
    }

    // Internal access for the flow preview (same assembly), which must never write to these.
    internal float[] FieldArray(int t) => _fields[t];
    internal float[] CostArray => _cost;
}
