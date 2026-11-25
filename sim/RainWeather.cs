using System;

namespace TideToll.Sim;

/// <summary>
/// Rain and ground wetness (the toy's weather.ts, round 4). The weather has its own random stream, seeded from the map
/// seed, so rolling for rain never moves the walkers' stream: a run with rain off draws nothing from it. Rain is rolled
/// once at the start of every in-game day (two numbers a day, the chance and the strength, even when catching up).
/// Plain double arithmetic, in the toy's order. Not the river's spring flood or ice (RiverSeasons.cs): those have their
/// own stream and run only with the river rules, while this runs only without them.
/// </summary>
public sealed class RainWeather
{
    private readonly SimParams _p;
    private Mulberry32 _rng;
    /// <summary>Day index (0-based) last rolled.</summary>
    private int _day = -1;
    /// <summary>Wetness left over from earlier rain when a new shower began.</summary>
    private double _carried;

    /// <summary>How hard it is raining now, 0..1 (fades in and out over RainRampSimSec; drawing and sound only).</summary>
    public double Rain { get; private set; }
    /// <summary>How wet the ground is, 0..1: 1 while raining and through the mud days, falling to 0 as it dries.</summary>
    public double Wetness { get; private set; }
    /// <summary>Whether it is raining now (the rain day, or a forced shower).</summary>
    public bool Raining { get; private set; }
    /// <summary>Strength of the current shower, 0.5..1 (drawing and sound only).</summary>
    public double Strength { get; private set; }
    /// <summary>Sim time the current or last shower started and stops.</summary>
    public double RainStart { get; private set; } = -1e9;
    public double RainEnd { get; private set; } = -1e9;
    /// <summary>Sim time the mud is gone (dry ground).</summary>
    public double MudUntil { get; private set; } = -1e9;
    /// <summary>Rain days so far (rolled or forced).</summary>
    public int RainDays { get; private set; }

    /// <summary>
    /// The stream's seed: <c>((seed * 2654435761) ^ 0x5eed) &gt;&gt;&gt; 0</c> with the toy's arithmetic. The toy multiplies in
    /// doubles, so above about 3.39 million (2^53 / 2654435761) the product is rounded to the nearest double before
    /// <c>^</c> takes its low 32 bits (ToInt32). The same here: the rounded product is an integer below 2^63, so the
    /// conversion to long is exact. Below that bound this equals the exact integer product.
    /// </summary>
    public static uint StreamSeed(int seed) => unchecked((uint)(long)((double)seed * 2654435761.0) ^ 0x5eedu);

    public RainWeather(int seed, SimParams p)
    {
        _p = p;
        _rng = new Mulberry32(StreamSeed(seed));
    }

    /// <summary>Advances the weather to sim time <paramref name="t"/>. Returns true when a shower started this tick.</summary>
    public bool Update(double t)
    {
        var p = _p;
        int day = (int)Math.Floor(t / p.DaySimSec);
        bool started = false;
        while (_day < day)
        {
            _day++;
            int season = (int)Math.Floor(_day * p.DaySimSec / p.SeasonSimSec) % 4;
            double roll = _rng.NextDouble(), strength = 0.5 + 0.5 * _rng.NextDouble();
            if (_day == day && roll < p.RainChance[season])
            {
                double start = _day * p.DaySimSec;
                Begin(start, start + p.DaySimSec, start + p.MudDays[season] * p.DaySimSec, strength);
                started = true;
            }
        }
        Raining = t >= RainStart && t < RainEnd;
        double ramp = p.RainRampSimSec;
        Rain = Raining ? Strength * Math.Min(1, Math.Min((t - RainStart) / ramp, (RainEnd - t) / ramp)) : 0;
        if (t >= MudUntil) Wetness = 0;
        else
        {
            double up = Math.Min(1, Math.Max(0, (t - RainStart) / p.WetUpSimSec));
            double dry = Math.Min(1, Math.Max(0, (MudUntil - t) / p.DrySimSec));
            // A new shower on wet ground starts from the wetness the ground still has.
            Wetness = Math.Max(Math.Max(_carried * (1 - up), Math.Min(up, dry)), Raining ? up : 0);
        }
        return started;
    }

    private void Begin(double start, double end, double mudUntil, double strength)
    {
        _carried = Wetness;
        RainStart = start;
        RainEnd = end;
        MudUntil = Math.Max(MudUntil, mudUntil);
        Strength = strength;
        RainDays++;
    }

    /// <summary>
    /// Starts rain at <paramref name="t"/> for <paramref name="simSec"/> seconds, with mud lasting as a rain day of this
    /// season would after it stops. Draws nothing, so the daily rolls that follow are unchanged.
    /// </summary>
    public void StartRain(double t, double simSec, double strength = 1)
    {
        var p = _p;
        int season = (int)Math.Floor(t / p.SeasonSimSec) % 4;
        Begin(t, t + simSec, t + simSec + (p.MudDays[season] - 1) * p.DaySimSec, strength);
        Update(t);
    }

    /// <summary>Stops the rain at <paramref name="t"/>; with <paramref name="dry"/> the mud goes too (otherwise it lasts its days out).</summary>
    public void StopRain(double t, bool dry = false)
    {
        if (RainEnd > t) RainEnd = t;
        if (dry) MudUntil = t;
        Update(t);
    }
}
