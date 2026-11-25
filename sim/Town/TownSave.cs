using System;
using System.Text.Json;

namespace TideToll.Sim.Town;

/// <summary>Saving: the whole TownState as JSON (the plan is rebuilt from the seed). Events not drained are not saved.</summary>
public static class TownSave
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true, WriteIndented = false };

    public static string Save(Town town) => JsonSerializer.Serialize(town.S, Options);

    /// <summary>Rebuilds a town from <see cref="Save"/>'s JSON. Throws on a version it does not know.</summary>
    public static Town Load(string json) => Load(json, null);

    /// <summary>As Load, reusing a plan already built for the same seed (saves the planner's work).</summary>
    public static Town Load(string json, TownPlan? plan)
    {
        var s = JsonSerializer.Deserialize<TownState>(json, Options) ?? throw new InvalidOperationException("empty town save");
        if (s.Version != CurrentVersion) throw new InvalidOperationException($"town save version {s.Version} is not {CurrentVersion}");
        if (plan == null || plan.Seed != s.Seed) plan = TownPlanner.Build(s.Seed);
        if (s.RoadOpen.Length != plan.Roads.Length || s.PlotGiven.Length != plan.Plots.Length)
            throw new InvalidOperationException("town save does not match the plan for its seed");
        if (s.Achieved.Length != TownAchievements.All.Length)
        {
            var a = new bool[TownAchievements.All.Length];
            Array.Copy(s.Achieved, a, Math.Min(a.Length, s.Achieved.Length));
            s.Achieved = a;
        }
        return new Town(plan, s);
    }
}
