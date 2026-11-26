using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Steps the town simulation at its own tick rate (TownRules.TickHz) whatever the frame rate, from a fixed-step
/// accumulator, and hands the drawing layers an interpolation factor (<see cref="Alpha"/>) between the last two ticks.
/// <see cref="Speed"/> multiplies game time (tests and fast play); a frame never runs more than
/// <see cref="MaxStepsPerFrame"/> steps, so a slow frame cannot snowball. Events are drained once per frame into
/// <see cref="Events"/> and announced with <see cref="Drained"/>; every layer reads the same list.
/// </summary>
public partial class TownDriver : Node
{
    public Town Town { get; private set; } = null!;
    public int Seed { get; private set; } = 1;
    /// <summary>Game seconds per real second (1 normally; tests use up to 64).</summary>
    public double Speed { get; private set; } = 1;
    public bool Paused { get; private set; }
    /// <summary>Interpolation between the previous tick (0) and the current one (1).</summary>
    public float Alpha { get; private set; } = 1f;
    /// <summary>Upper bound of steps per frame (64 at the highest test speed and 60 fps would need 11).</summary>
    public int MaxStepsPerFrame = 200;
    /// <summary>Real seconds since start, for animation that must run while paused too.</summary>
    public double AnimTime { get; private set; }
    /// <summary>Events of this frame (cleared at the start of the next).</summary>
    public readonly List<TownEvent> Events = new(64);
    public event Action? Drained;
    /// <summary>A new town was made (reset).</summary>
    public event Action? Replaced;
    public long StepsRun { get; private set; }

    private double _acc;
    /// <summary>Longest frame counted in full (seconds), and the most steps carried over to later frames.</summary>
    public const double MaxFrameSeconds = 1.0;
    public int MaxBacklogSteps = 2000;
    /// <summary>Steps owed and not yet run (tests).</summary>
    public double Backlog => _acc / TownRules.Dt;

    public void Reset(int seed)
    {
        Seed = seed;
        Town = new Town(seed);
        _acc = 0;
        Alpha = 1f;
        Replaced?.Invoke();
    }

    /// <summary>Takes over a town made elsewhere (tests: a town the bot has played to the end of the age).</summary>
    public void Adopt(Town t)
    {
        Town = t;
        Seed = t.Plan.Seed;
        _acc = 0;
        Alpha = 1f;
        Replaced?.Invoke();
    }

    /// <summary>
    /// Plays the town with a bot at once, step by step, until <paramref name="until"/> holds or
    /// <paramref name="maxTicks"/> pass; the events are drained by the next frame as usual. Returns the ticks run.
    /// </summary>
    public int RunBot(TownBot bot, Func<Town, bool> until, int maxTicks)
    {
        int n = 0;
        while (n < maxTicks && !until(Town)) { Town.Step(); bot.Act(Town); n++; StepsRun++; }
        return n;
    }

    public void SetSpeed(double s) => Speed = Math.Clamp(s, 0.25, 256);
    public void SetPaused(bool p) => Paused = p;

    /// <summary>Runs whole game seconds at once (tests and the perf scene), draining events into the frame list.</summary>
    public void Advance(double seconds)
    {
        int n = (int)Math.Round(seconds * TownRules.TickHz);
        for (int i = 0; i < n; i++) { Town.Step(); StepsRun++; }
    }

    public override void _Process(double delta)
    {
        AnimTime += delta;
        Events.Clear();
        if (Town == null) return;
        if (!Paused)
        {
            // Round 4: real time counts up to a second a frame (a longer stall, such as a dragged window, is not
            // replayed), and steps a frame cannot run are carried to the next frames instead of being thrown away;
            // the backlog is bounded so a machine that can never keep up does not snowball.
            _acc += Math.Min(delta, MaxFrameSeconds) * Speed;
            int steps = 0;
            while (_acc >= TownRules.Dt && steps < MaxStepsPerFrame)
            {
                Town.Step();
                _acc -= TownRules.Dt;
                steps++;
            }
            _acc = Math.Min(_acc, MaxBacklogSteps * TownRules.Dt);
            StepsRun += steps;
            Alpha = (float)Math.Clamp(_acc / TownRules.Dt, 0, 1);
        }
        Town.DrainEvents(Events);
        Drained?.Invoke();
    }
}
