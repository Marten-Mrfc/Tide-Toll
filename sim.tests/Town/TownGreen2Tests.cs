using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Green pass 2: Regulars, the first age's ladder of small steps (docs/plans/town-sim-api.md "Added in the green pass").</summary>
public class TownGreen2Tests
{
    private static ShopItem Find(T t, ShopItemKind k)
    {
        var items = new List<ShopItem>();
        t.ShopItems(items);
        return items.Single(x => x.Kind == k);
    }

    [Fact]
    public void RegularsAreOnSaleFromTheStartAndEachMakesDeliveriesPayMore()
    {
        var t = new T(1);
        var it = Find(t, ShopItemKind.Regulars);
        Assert.Equal("Regulars", it.Name);
        Assert.False(it.Locked);
        Assert.False(it.Finished);
        Assert.Equal(Math.Ceiling(TownRules.RegularsMinutes * TownRules.RegularsRateMin), it.Price);
        // Worth: the coin a minute it adds at a rate (the shop rounds to 0.1, so at the very start it reads 0.0).
        Assert.Equal(100 * TownRules.RegularsStep / t.CoinBonus, t.WorthAt(ShopItemKind.Regulars, -1, 100), 9);
        double bonus = t.CoinBonus;
        t.S.Coin = 1000;
        double p0 = t.RegularsPrice;
        Assert.True(t.BuyRegulars());
        Assert.Equal(1, t.Regulars);
        Assert.Equal(1000 - p0, t.Coin, 6);
        Assert.Equal(bonus + TownRules.RegularsStep, t.CoinBonus, 9);
        Assert.Equal(1 + TownRules.RegularsStep, t.RegularsFactor, 9);
        // The ladder: each regular at this level is RegularsGrowth dearer.
        Assert.Equal(Math.Ceiling(TownRules.RegularsMinutes * TownRules.RegularsRateMin * TownRules.RegularsGrowth), t.RegularsPrice);
        Assert.True(t.Buy(Find(t, ShopItemKind.Regulars)));
        Assert.Equal(2, t.Regulars);
        Assert.Equal(2, t.RegularsThisLevel);
    }

    [Fact]
    public void TheLadderStartsAgainAtEachLevelFromTheBestRate()
    {
        var t = new T(2);
        var bot = new TownBot(BotKind.Cheapest, 2);
        int level = t.Level;
        bool sawRestart = false;
        for (int i = 0; i < 20 * 60 * TownRules.TickHz && !t.HasCharter; i++)
        {
            t.Step(); bot.Act(t);
            if (t.Level != level)
            {
                level = t.Level;
                if (t.RegularsThisLevel == 0)
                {
                    sawRestart = true;
                    Assert.Equal(Math.Ceiling(TownRules.RegularsMinutes * Math.Max(TownRules.RegularsRateMin, t.S.RegularsRate)), t.RegularsPrice);
                    Assert.True(t.S.RegularsRate >= t.S.MaxCoinPerMinute - 1e-9 || t.S.RegularsRate >= TownRules.RegularsRateMin);
                }
            }
        }
        Assert.True(sawRestart);
        Assert.True(t.Regulars > 0);
    }

    [Fact]
    public void RegularsAreSavedAndAnOldSaveLoadsWithNone()
    {
        var t = new T(3);
        t.S.Coin = 1000;
        Assert.True(t.BuyRegulars());
        Assert.True(t.BuyRegulars());
        var back = TownSave.Load(TownSave.Save(t));
        Assert.Equal(2, back.Regulars);
        Assert.Equal(2, back.RegularsThisLevel);
        Assert.Equal(t.RegularsPrice, back.RegularsPrice);
        string old = TownSave.Save(new T(3)).Replace("\"Regulars\":0,", "").Replace("\"RegularsAtLevel\":0,", "").Replace("\"RegularsRate\":0,", "");
        Assert.DoesNotContain("Regulars", old);
        var o = TownSave.Load(old);
        Assert.Equal(0, o.Regulars);
        Assert.Equal(Math.Ceiling(TownRules.RegularsMinutes * TownRules.RegularsRateMin), o.RegularsPrice);
    }

    [Fact]
    public void RegularsAreNotSoldInTheSecondAgeButWhatWasWonStays()
    {
        var t = new T(4);
        var bot = new TownBot(BotKind.Payback, 4) { PlayOn = true };
        for (int i = 0; i < 45 * 60 * TownRules.TickHz && !t.InRiverAge; i++) { t.Step(); bot.Act(t); }
        Assert.True(t.InRiverAge);
        int won = t.Regulars;
        Assert.True(won > 0);
        var items = new List<ShopItem>();
        t.ShopItems(items);
        Assert.DoesNotContain(items, x => x.Kind == ShopItemKind.Regulars);
        Assert.False(t.BuyRegulars());
        Assert.Equal(won, t.Regulars);
        Assert.Equal(1 + TownRules.RegularsStep * won, t.RegularsFactor, 9);
    }
}
