using System;

namespace TideToll.Sim.Town;

public sealed class AchievementDef
{
    public string Id = "", Name = "", Description = "";
    public double Target;
    internal Func<Town, double> Value = _ => 0;
}

public struct AchievementView
{
    public int Index;
    public string Id, Name, Description;
    public double Progress;      // 0..1
    public double Value, Target;
    public bool Unlocked;
}

/// <summary>The first age's achievements (checked once per game second).</summary>
public static class TownAchievements
{
    private static AchievementDef A(string id, string name, string desc, double target, Func<Town, double> value) =>
        new() { Id = id, Name = name, Description = desc, Target = target, Value = value };

    public static readonly AchievementDef[] All =
    {
        A("first_coin", "First coin", "Earn your first coin.", 1, t => t.CoinEarned),
        A("purse_1k", "A full purse", "Earn 1,000 coin in all.", 1000, t => t.CoinEarned),
        A("treasury_20k", "Town treasury", "Earn 20,000 coin in all.", 20000, t => t.CoinEarned),
        A("trips_100", "Well trodden", "Finish 100 trips.", 100, t => t.Trips),
        A("trips_1000", "Busy lanes", "Finish 1,000 trips.", 1000, t => t.Trips),
        A("water_200", "Water carrier", "Bring home 200 Water.", 200, t => t.Delivered(Good.Water)),
        A("grain_200", "Harvest home", "Bring in 200 Grain.", 200, t => t.Delivered(Good.Grain)),
        A("flour_50", "Daily bread", "Sell 50 Flour at the market.", 50, t => t.Delivered(Good.Flour)),
        A("villagers_5", "Hamlet", "Have 5 villagers.", 5, t => t.VillagerCount),
        A("villagers_10", "Village", "Have 10 villagers.", 10, t => t.VillagerCount),
        A("first_pave", "First gravel", "Pave a road.", 1, t => t.RoadsPaved),
        A("cobbles", "Cobbled", "Lay cobbles on a road.", 1, t => t.CountSurface(Surface.Cobbles)),
        A("all_paved", "Every road paved", "Pave every road of the age.", 1, t => t.Plan.Roads.Length == 0 ? 0 : (double)t.CountPaved() / t.Plan.Roads.Length),
        A("worn_5", "Desire lines", "Wear in 5 roads.", 5, t => t.CountWornIn()),
        A("bell_10", "Ring out", "Ring the bell 10 times.", 10, t => t.BellRings),
        A("pace_10", "Quick step", "Reach Pace 10.", 10, t => t.PaceLevel),
        A("handcart", "Handcart", "Buy every Load upgrade.", 3, t => t.LoadLevel),
        A("level_3", "Field and furrow", "Reach level 3.", 3, t => t.Level),
        A("level_6", "Closing the loop", "Reach level 6.", 6, t => t.Level),
        A("level_9", "Growing town", "Reach level 9.", 9, t => t.Level),
        A("charter", "Chartered", "Receive the town charter.", 1, t => t.HasCharter ? 1 : 0),
        A("market", "Market day", "Buy the market stall.", 1, t => t.OfferOf(OfferKind.MarketStall) is int m && m >= 0 && t.OfferBought(m) ? 1 : 0),
        A("mill", "The miller", "Build the mill.", 1, t => t.OfferOf(OfferKind.Mill) is int m && m >= 0 && t.OfferBought(m) ? 1 : 0),
        A("rate_300", "Thriving", "Earn 300 coin in one minute.", 300, t => t.MaxCoinPerMinute),
    };
}

public sealed partial class Town
{
    public long Delivered(Good g) => S.Delivered[(int)g];
    public double MaxCoinPerMinute => S.MaxCoinPerMinute;

    public int CountSurface(Surface s)
    {
        int c = 0;
        for (int r = 0; r < S.RoadSurface.Length; r++) if (S.RoadSurface[r] == s) c++;
        return c;
    }

    public int CountPaved() => Plan.Roads.Length - CountSurface(Surface.Track);

    public int CountWornIn()
    {
        int c = 0;
        for (int r = 0; r < S.RoadWear.Length; r++) if (S.RoadWear[r] >= 1) c++;
        return c;
    }

    public int AchievementCount => TownAchievements.All.Length;

    public AchievementView Achievement(int i)
    {
        var a = TownAchievements.All[i];
        double v = a.Value(this);
        return new AchievementView
        {
            Index = i, Id = a.Id, Name = a.Name, Description = a.Description, Value = v, Target = a.Target,
            Progress = S.Achieved[i] ? 1 : Math.Clamp(v / a.Target, 0, 1), Unlocked = S.Achieved[i],
        };
    }

    private void CheckAchievements()
    {
        var all = TownAchievements.All;
        for (int i = 0; i < all.Length; i++)
        {
            if (S.Achieved[i]) continue;
            if (all[i].Value(this) >= all[i].Target - 1e-9)
            {
                S.Achieved[i] = true;
                Emit(new TownEvent { Kind = TownEventKind.AchievementUnlocked, Index = i, Text = all[i].Name });
            }
        }
    }
}
