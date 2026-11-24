namespace TideToll.Sim;

/// <summary>
/// Tunables of the simulation. Phase 0 holds only the map and time scale; the wear and walker
/// values join when the world model is ported from the toy (prototypes/desire-lines/src/sim/params.ts).
/// A record so tests can change one value with `with` and leave the defaults alone.
/// </summary>
public sealed record SimParams
{
    public static readonly SimParams Default = new();

    // Map. One cell is 2 m, so a 128-cell side is 256 m. The game default is 512 x 384.
    public int GridWidth { get; init; } = 128;
    public int GridHeight { get; init; } = 128;
    public double CellMeters { get; init; } = 2.0;

    // Time. A fixed 10 Hz tick; faster game speeds run more ticks, never longer ones.
    public int TickHz { get; init; } = 10;
    public double DaySimSec { get; init; } = 15;    // one in-game day at 1x
    public double SeasonSimSec { get; init; } = 60; // one season = 4 days

    // The toy compresses time: walkers cover 3 cells per sim second instead of the
    // real 0.67, so a small town still wears trails within a few days.
    public double WalkSpeedCellsPerSec { get; init; } = 3.0;

    public Grid Grid => new(GridWidth, GridHeight);
    public int TicksPerDay => (int)(DaySimSec * TickHz);
    public int TicksPerSeason => (int)(SeasonSimSec * TickHz);
    public double SecondsPerTick => 1.0 / TickHz;
}
