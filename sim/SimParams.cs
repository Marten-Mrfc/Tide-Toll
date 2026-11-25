using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// Every tunable of the simulation, ported one for one from the toy's params.ts, with the same names in PascalCase.
/// A record so tests can change one value with `with` and leave the defaults alone. The world copies what its hot
/// path needs into fields at construction, so reading these properties never costs anything per tick.
/// Two presets: <see cref="Legacy"/>, the round-2 toy (snapshot a057b50; the property initialisers below), and
/// <see cref="Toy"/>, the finished toy's trail model (rounds 3 to 5). <see cref="Default"/> is Legacy until the
/// sync plan's step A9 (docs/plans/sync-plan.md) flips it.
/// </summary>
public sealed record SimParams
{
    /// <summary>The round-2 toy (a057b50): no explorers, no detour cap, no bridge flow discount.</summary>
    public static readonly SimParams Legacy = new();

    /// <summary>
    /// The finished toy's trail model (params.ts, rounds 3 to 5): wearPerStep 2.2, sideShare 0.4, minSurface 0.45,
    /// fordCostMul 4, explorers 10%, frugal discount 0.3, detour cap 1.25, bridge flow discount on.
    /// </summary>
    public static readonly SimParams Toy = Legacy with
    {
        WearPerStep = 2.2, SideShare = 0.4, MinSurface = 0.45, FordCostMul = 4.0,
        ExplorerShare = 0.1, FrugalDisc = 0.3, DetourCap = 1.25, BridgeFlow = 1,
        PreviewV2 = true,
    };

    /// <summary>What <c>new World(seed)</c> uses: <see cref="Legacy"/> until step A9.</summary>
    public static readonly SimParams Default = Legacy;

    // Map. One cell is 2 m, so a 128-cell side is 256 m. The map generator is tuned for 128 x 128;
    // the world needs a square grid (GridWidth == GridHeight).
    public int GridWidth { get; init; } = 128;
    public int GridHeight { get; init; } = 128;
    public double CellMeters { get; init; } = 2.0;

    // Time. A fixed 10 Hz tick; faster game speeds run more ticks, never longer ones.
    public int TickHz { get; init; } = 10;
    public double DaySimSec { get; init; } = 15;    // one in-game day at 1x
    public double SeasonSimSec { get; init; } = 60; // one season = 4 days

    // Walkers. The toy compresses time: 3 cells per sim second instead of the real 0.67,
    // so a small town still wears trails within a few days.
    public int DefaultWalkers { get; init; } = 200;
    public int MinWalkers { get; init; } = 50; // unused by the simulation, as in the toy
    public int MaxWalkers { get; init; } = 500;
    public double WalkSpeedCellsPerSec { get; init; } = 3.0;
    public double DwellSimSec { get; init; } = 6;
    public double DwellJitterLow { get; init; } = 0.5;
    public double DwellJitterHigh { get; init; } = 1.5;
    public double TripMixWell { get; init; } = 0.35;
    public double TripMixField { get; init; } = 0.3;
    public double TripMixMarket { get; init; } = 0.35;
    public double StuckSimSec { get; init; } = 5;
    public double RetrySimSec { get; init; } = 5;

    // Wear. W in 0..100; gain W += I * (1 - W/100) on entering a cell.
    public double WearPerStep { get; init; } = 2.4;
    public double SideShare { get; init; } = 0.3;
    // Ground-state thresholds: grass < 10 <= trampled < 25 <= trail < 50 <= track; hollow at 85.
    public double ThresholdTrampled { get; init; } = 10;
    public double ThresholdTrail { get; init; } = 25;
    public double ThresholdTrack { get; init; } = 50;
    public double ThresholdHollow { get; init; } = 85;
    public double HollowHoldSimSec { get; init; } = 2400;
    /// <summary>Routing cost by ground state (used only when TrailDiscount is 0).</summary>
    public IReadOnlyList<double> SurfaceCost { get; init; } = new[] { 1.0, 0.95, 0.85, 0.75, 0.72 };
    public IReadOnlyList<double> StateSpeedMul { get; init; } = new[] { 1.0, 1.0, 1.1, 1.2, 1.2 };
    public double TrailDiscount { get; init; } = 0.3;
    public double DiscScale { get; init; } = 80;
    public double DiscPow { get; init; } = 3;
    public double FlowDiscount { get; init; } = 0.5;
    public double FlowScale { get; init; } = 40;
    public double FlowHalfLife { get; init; } = 20;
    public double MinSurface { get; init; } = 0.6;
    public double HalfLifeLow { get; init; } = 60;
    public double HalfLifeMid { get; init; } = 120;
    public double HalfLifeHigh { get; init; } = 240;
    public double HalfLifeHollow { get; init; } = 2400;
    public double DecayStepSimSec { get; init; } = 1;

    // Route cost.
    public double TasteNoise { get; init; } = 0.08;
    public double StepNoise { get; init; } = 0.06;
    public double StaticNoise { get; init; } = 0.04;
    public double CropPenalty { get; init; } = 0.5;
    public int Lookahead { get; init; } = 20;
    public double ShortcutSlack { get; init; } = 1.0;
    public double FordCostMul { get; init; } = 3.0;
    public double FordSpeedMul { get; init; } = 0.4;
    public double BridgeCost { get; init; } = 0.8;
    public double BridgeSpeedMul { get; init; } = 1.2;
    /// <summary>
    /// Frugal walkers (toy round 3): a second view of the ground for walkers who weigh distance more than company. Their
    /// cost is 1 + (surface - 1) x FrugalDisc. A trip is frugal if the walker is an explorer (a fixed share of walkers, by
    /// Hash2(id, 77)) or if the ordinary route from where it stands is longer than DetourCap x the straight distance (0 off).
    /// With ExplorerShare and DetourCap both 0 the frugal view is never built.
    /// </summary>
    public double ExplorerShare { get; init; } = 0;
    public double FrugalDisc { get; init; } = 0.3;
    public double DetourCap { get; init; } = 0;
    /// <summary>1: a bridge deck is made cheaper by recent traffic like any lane (floored at MinSurface); 0: fixed BridgeCost.</summary>
    public double BridgeFlow { get; init; } = 0;
    // Steering tools (toy rounds 3 and 4): gravel and gates in fences.
    public double GravelCost { get; init; } = 0.6;
    public double GravelSpeedMul { get; init; } = 1.15;
    /// <summary>Floor on a busy gravel cell's cost (MinSurface for worn ground); wear under gravel never decays below trail.</summary>
    public double GravelFloor { get; init; } = 0.45;
    /// <summary>Added to a gate cell's step cost after crowding (half each on the two cells of a stair-corner gate).</summary>
    public double GateCost { get; init; } = 1.5;
    public double GateSpeedMul { get; init; } = 0.4;
    public double FieldRefreshSimSec { get; init; } = 5;
    public int UrgentFieldsPerTick { get; init; } = 1;

    // Crowding (Weidmann): v = v0 (1 - exp(-g (1/rho - 1/rhoMax))), rho in people per m^2.
    public double CrowdStrength { get; init; } = 1.0;
    public double CrowdAvgSimSec { get; init; } = 10;
    public double CrowdMaxCostMul { get; init; } = 3;
    public double WeidmannGamma { get; init; } = 1.913;
    public double WeidmannRhoMax { get; init; } = 5.4;
    public double CellAreaM2 { get; init; } = 4;

    /// <summary>
    /// The flow preview's version (see <see cref="FlowPredictor"/>): false is round 2's, true the finished toy's (both
    /// kinds of walker, pairs from 0.2 trips a day, the mean cost change in the readout, the gravel note).
    /// </summary>
    public bool PreviewV2 { get; init; }

    // Flow preview bookkeeping: trips per (house, destination) decay with this time constant in days.
    public double TripRateDays { get; init; } = 3;

    // Metrics.
    public double MetricWindowDays { get; init; } = 2;
    public int MetricBuckets { get; init; } = 4;
    public int FirstTrailCells { get; init; } = 10;
    public double TrunkTopShare { get; init; } = 0.1;

    /// <summary>The river rules (boats, crossings, money). Null, the default, leaves them out entirely.</summary>
    public RiverRules? River { get; init; }

    public Grid Grid => new(GridWidth, GridHeight);
    public int TicksPerDay => (int)(DaySimSec * TickHz);
    public int TicksPerSeason => (int)(SeasonSimSec * TickHz);
    public double SecondsPerTick => 1.0 / TickHz;
    /// <summary>Whether walkers ever use the frugal view (otherwise its costs and fields are not built).</summary>
    public bool UseFrugal => ExplorerShare > 0 || DetourCap > 0;

    /// <summary>The ground state of a cell with wear w (the toy's groundOf).</summary>
    public Ground GroundOf(double w, bool hollow)
    {
        if (w < ThresholdTrampled) return Ground.Grass;
        if (w < ThresholdTrail) return Ground.Trampled;
        if (w < ThresholdTrack) return Ground.Trail;
        return hollow ? Ground.Hollow : Ground.Track;
    }

    internal void Validate()
    {
        if (GridWidth != GridHeight) throw new ArgumentException("The world needs a square grid.");
        if (GridWidth < 64) throw new ArgumentException("The map generator needs at least 64 cells per side.");
        if (SurfaceCost.Count != 5 || StateSpeedMul.Count != 5) throw new ArgumentException("SurfaceCost and StateSpeedMul need one value per Ground state.");
        if (MaxWalkers < 0 || MetricBuckets < 1 || Lookahead < 1) throw new ArgumentException("Invalid walker, bucket or lookahead count.");
    }
}

/// <summary>Ground state of a cell, from its wear.</summary>
public enum Ground : byte { Grass = 0, Trampled = 1, Trail = 2, Track = 3, Hollow = 4 }
