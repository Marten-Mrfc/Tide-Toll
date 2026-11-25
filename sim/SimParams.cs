using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// Every tunable of the simulation, ported one for one from the toy's params.ts (snapshot a057b50),
/// with the same names in PascalCase and the same values. A record so tests can change one value with
/// `with` and leave the defaults alone. The world copies what its hot path needs into fields at
/// construction, so reading these properties never costs anything per tick.
/// </summary>
public sealed record SimParams
{
    public static readonly SimParams Default = new();

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
    public double FieldRefreshSimSec { get; init; } = 5;
    public int UrgentFieldsPerTick { get; init; } = 1;

    // Crowding (Weidmann): v = v0 (1 - exp(-g (1/rho - 1/rhoMax))), rho in people per m^2.
    public double CrowdStrength { get; init; } = 1.0;
    public double CrowdAvgSimSec { get; init; } = 10;
    public double CrowdMaxCostMul { get; init; } = 3;
    public double WeidmannGamma { get; init; } = 1.913;
    public double WeidmannRhoMax { get; init; } = 5.4;
    public double CellAreaM2 { get; init; } = 4;

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
