using System;

namespace TideToll.Sim.Town;

/// <summary>How a measured player handles the trades (the bots buy as before; this only sets trades).</summary>
public enum TradePlay : byte
{
    LeftAlone = 0,        // the town fills the trades; nobody touches them (as shipped)
    Careful = 1,          // every minute sets each trade's count to the mix the best-pay rule would choose now
    CarelessCarriers = 2, // every villager a carrier (set on its card as it arrives)
    CarelessHelpers = 3,  // the town's filling off: every villager a Helper
}

/// <summary>
/// The careful and careless players of the trades' tuning (docs/plans/n7/trades-build-sheet.md part 1 item 7, part 4).
/// Call <see cref="Act"/> once per tick after Town.Step, beside a TownBot; it acts only through the player's calls.
/// </summary>
public sealed class TownTradePlayer
{
    public TradePlay Play { get; }
    /// <summary>The careful player's interval in game seconds.</summary>
    public const int CarefulEvery = 60;
    private readonly int[] _want = new int[Town.TradeCount];

    public TownTradePlayer(TradePlay play) { Play = play; }

    public static string Name(TradePlay p) => p switch
    {
        TradePlay.Careful => "careful", TradePlay.CarelessCarriers => "careless (all carriers)",
        TradePlay.CarelessHelpers => "careless (all Helpers)", _ => "left alone",
    };

    public void Act(Town t)
    {
        switch (Play)
        {
            case TradePlay.CarelessHelpers:
                if (t.TownFillsTrades) t.SetTownFillsTrades(false);
                break;
            case TradePlay.CarelessCarriers:
                if (t.Tick % TownRules.TickHz != 0) return;
                for (int i = 0; i < t.VillagerCount; i++)
                    if (t.S.Villagers[i].Trade != Trade.Water || !t.S.Villagers[i].TradeByPlayer) t.SetVillagerTrade(i, Trade.Water);
                break;
            case TradePlay.Careful:
                if (t.Tick % (CarefulEvery * TownRules.TickHz) != 0) return;
                SetMix(t);
                break;
        }
    }

    /// <summary>Counts each trade as the best-pay rule would choose for every villager now (a job of no trade: a Helper),
    /// then sets the counts that fall first and those that rise after.</summary>
    private void SetMix(Town t)
    {
        Array.Clear(_want);
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var tr = Town.TradeOfJob(t.BestPayJob(i));
            if (tr != Trade.None && !t.TradeOpen(tr)) tr = Trade.None;
            _want[(int)tr]++;
        }
        for (int k = 1; k < Town.TradeCount; k++)
            if (t.TradeOpen((Trade)k) && t.TradeInfo((Trade)k).Members > _want[k]) t.SetTradeCount((Trade)k, _want[k]);
        for (int k = 1; k < Town.TradeCount; k++)
            if (t.TradeOpen((Trade)k) && t.TradeInfo((Trade)k).Members <= _want[k]) t.SetTradeCount((Trade)k, _want[k]);
    }
}
