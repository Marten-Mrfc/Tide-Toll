using System;
using System.Text.Json;

namespace TideToll.Sim.Town;

/// <summary>Saving: the whole TownState as JSON (the plan is rebuilt from the seed). Events not drained are not saved.</summary>
public static class TownSave
{
    /// <summary>Version 3 since the second age: a town in it saves as 3. A town that never rang it in still saves as 2,
    /// exactly as before; both load.</summary>
    public const int CurrentVersion = 3;
    public const int FirstAgeVersion = 2;

    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true, WriteIndented = false };

    public static string Save(Town town) => JsonSerializer.Serialize(town.S, Options);

    /// <summary>Rebuilds a town from <see cref="Save"/>'s JSON. Throws on a version it does not know.</summary>
    public static Town Load(string json) => Load(json, null);

    /// <summary>As Load, reusing a plan already built for the same seed (saves the planner's work).</summary>
    public static Town Load(string json, TownPlan? plan)
    {
        var s = JsonSerializer.Deserialize<TownState>(json, Options) ?? throw new InvalidOperationException("empty town save");
        if (s.Version != CurrentVersion && s.Version != FirstAgeVersion) throw new InvalidOperationException($"town save version {s.Version} is not {FirstAgeVersion} or {CurrentVersion}");
        if (s.Version == FirstAgeVersion && s.River != null) throw new InvalidOperationException("a version 2 town save cannot hold the second age");
        if (plan == null || plan.Seed != s.Seed) plan = TownPlanner.Build(s.Seed);
        if (s.RoadOpen.Length != plan.Roads.Length || s.PlotGiven.Length != plan.Plots.Length)
            throw new InvalidOperationException("town save does not match the plan for its seed");
        if (s.Achieved.Length != TownAchievements.All.Length)
        {
            var a = new bool[TownAchievements.All.Length];
            Array.Copy(s.Achieved, a, Math.Min(a.Length, s.Achieved.Length));
            s.Achieved = a;
        }
        // n7 step 3: saves from before the second age's trades hold four entries by trade; the new ones load at their
        // defaults (nobody in them, their counts the town's, no earnings yet).
        s.TradeCountByPlayer = Grown(s.TradeCountByPlayer);
        s.TradeWant = Grown(s.TradeWant);
        s.TradeEarn = Grown(s.TradeEarn);
        s.TradeAt = Grown(s.TradeAt);
        return new Town(plan, s);
    }

    private static T[] Grown<T>(T[]? a)
    {
        if (a != null && a.Length == Town.TradeCount) return a;
        var n = new T[Town.TradeCount];
        if (a != null) Array.Copy(a, n, Math.Min(a.Length, n.Length));
        return n;
    }
}
