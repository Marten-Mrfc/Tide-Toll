using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public enum ShopItemKind : byte { Villager = 0, Pace = 1, Load = 2, Pave = 3, Building = 4, Bell = 5, NextAge = 6 }

/// <summary>One thing the player can buy (or press). Index: offer id for Building, road id for Pave (the suggested road), else -1.</summary>
public struct ShopItem
{
    public ShopItemKind Kind;
    public int Index;
    public string Name;
    /// <summary>Levels bought on this track so far (Pave: roads paved; Building: 0 or 1).</summary>
    public int Level;
    public double Price;
    public bool Affordable;
    /// <summary>Empty when it can be bought now, else why not, in words for the player.</summary>
    public string Reason;
    public double Renown;
}

/// <summary>The shop: prices, purchases, renown, perks, the bell and the next age.</summary>
public sealed partial class Town
{
    private static readonly string[] PerkNames = { "Good neighbours", "Light feet", "", "Mason's eye" };

    public double VillagerPrice => TownRules.VillagerBase * Math.Pow(TownRules.VillagerGrowth, S.VillagersBought);
    public double PacePrice => TownRules.PaceBase * Math.Pow(TownRules.PaceGrowth, S.PaceLevel);
    public double LoadPrice => S.LoadLevel < TownRules.LoadPrices.Length ? TownRules.LoadPrices[S.LoadLevel] : double.PositiveInfinity;
    public int HousingRoom => Housing - S.Villagers.Length;

    /// <summary>Surface the road would get if paved now (Track when it cannot be paved further).</summary>
    public Surface NextSurface(int road) => S.RoadSurface[road] switch
    {
        Surface.Track => Surface.Gravel,
        Surface.Gravel when S.CobblesUnlocked => Surface.Cobbles,
        _ => Surface.Track,
    };

    public double PavePrice(int road)
    {
        var next = NextSurface(road);
        double per = next == Surface.Cobbles ? TownRules.CobblesPerCell : TownRules.GravelPerCell;
        return Math.Ceiling(Plan.Roads[road].Length * per * Math.Pow(TownRules.PaveGrowth, S.Paved));
    }

    /// <summary>Whether the road can be paved now, ignoring coin; reason in words if not.</summary>
    public bool CanPave(int road, out string reason)
    {
        reason = "";
        if (road < 0 || road >= Plan.Roads.Length || !S.RoadOpen[road]) { reason = "No such road yet"; return false; }
        if (S.RoadWear[road] < 1) { reason = "Not worn in yet"; return false; }
        if (S.RoadSurface[road] == Surface.Cobbles) { reason = "Already cobbled"; return false; }
        if (S.RoadSurface[road] == Surface.Gravel && !S.CobblesUnlocked) { reason = "Cobbles come later"; return false; }
        return true;
    }

    /// <summary>The busiest road that can be paved now (most footsteps; lowest id on a tie), or -1.</summary>
    public int SuggestedPave()
    {
        int best = -1;
        for (int r = 0; r < Plan.Roads.Length; r++)
            if (CanPave(r, out _) && (best < 0 || S.RoadSteps[r] > S.RoadSteps[best])) best = r;
        return best;
    }

    /// <summary>Fills <paramref name="into"/> (cleared first) with every purchasable thing, in a fixed order.</summary>
    public void ShopItems(List<ShopItem> into)
    {
        into.Clear();
        string room = HousingRoom > 0 ? "" : "No room: wait for a cottage";
        into.Add(Item(ShopItemKind.Villager, -1, "Villager", S.VillagersBought, VillagerPrice, room));
        into.Add(Item(ShopItemKind.Pace, -1, "Pace", S.PaceLevel, PacePrice, S.PaceLevel >= TownRules.PaceMax ? "Fastest pace reached" : ""));
        if (S.LoadLevel < TownRules.LoadPrices.Length)
            into.Add(Item(ShopItemKind.Load, -1, TownRules.LoadNames[S.LoadLevel], S.LoadLevel, LoadPrice, ""));
        int pr = SuggestedPave();
        if (pr >= 0) into.Add(Item(ShopItemKind.Pave, pr, NextSurface(pr) == Surface.Cobbles ? "Cobbles" : "Gravel", S.Paved, PavePrice(pr), ""));
        else into.Add(Item(ShopItemKind.Pave, -1, "Pave a road", S.Paved, 0, "No worn-in road to pave"));
        foreach (var o in Plan.Offers)
            if (S.OfferAvailable[o.Id] && !S.OfferBought[o.Id]) into.Add(Item(ShopItemKind.Building, o.Id, o.Name, 0, o.Price, ""));
    }

    private ShopItem Item(ShopItemKind k, int index, string name, int level, double price, string reason)
    {
        if (reason.Length == 0 && S.Coin < price) reason = "Not enough coin";
        return new ShopItem
        {
            Kind = k, Index = index, Name = name, Level = level, Price = price, Reason = reason,
            Affordable = reason.Length == 0, Renown = RenownFor(price),
        };
    }

    private static double RenownFor(double price) => Math.Max(1, Math.Round(price * TownRules.RenownPerCoin));

    /// <summary>Buys what the item names (the Index of a Pave item is the road). Same as the specific methods.</summary>
    public bool Buy(ShopItem item) => item.Kind switch
    {
        ShopItemKind.Villager => BuyVillager(),
        ShopItemKind.Pace => BuyPace(),
        ShopItemKind.Load => BuyLoad(),
        ShopItemKind.Pave => Pave(item.Index),
        ShopItemKind.Building => BuyOffer(item.Index),
        ShopItemKind.Bell => RingBell(),
        ShopItemKind.NextAge => RingNextAge(),
        _ => false,
    };

    public bool BuyVillager()
    {
        double p = VillagerPrice;
        if (HousingRoom <= 0 || !Pay(p)) return false;
        S.VillagersBought++;
        AddVillager();
        Bought(ShopItemKind.Villager, -1, p, S.VillagersBought);
        return true;
    }

    public bool BuyPace()
    {
        double p = PacePrice;
        if (S.PaceLevel >= TownRules.PaceMax || !Pay(p)) return false;
        S.PaceLevel++;
        Bought(ShopItemKind.Pace, -1, p, S.PaceLevel);
        return true;
    }

    public bool BuyLoad()
    {
        if (S.LoadLevel >= TownRules.LoadPrices.Length) return false;
        double p = LoadPrice;
        if (!Pay(p)) return false;
        S.LoadLevel++;
        Bought(ShopItemKind.Load, -1, p, S.LoadLevel);
        return true;
    }

    /// <summary>Paves one road (gravel on a worn-in track; cobbles on gravel once cobbles are given).</summary>
    public bool Pave(int road)
    {
        if (!CanPave(road, out _)) return false;
        double p = PavePrice(road);
        if (!Pay(p)) return false;
        var surface = NextSurface(road);
        S.RoadSurface[road] = surface;
        S.Paved++;
        Emit(new TownEvent { Kind = TownEventKind.RoadPaved, Index = road, Surface = surface });
        Bought(ShopItemKind.Pave, road, p, S.Paved);
        return true;
    }

    public bool BuyOffer(int offer)
    {
        if (offer < 0 || offer >= Plan.Offers.Length || !S.OfferAvailable[offer] || S.OfferBought[offer]) return false;
        var o = Plan.Offers[offer];
        if (!Pay(o.Price)) return false;
        S.OfferBought[offer] = true;
        RefreshWorkplaces();
        Emit(new TownEvent { Kind = TownEventKind.BuildingOpened, Index = offer, Plot = o.Plot, Text = o.Name });
        Bought(ShopItemKind.Building, offer, o.Price, 1);
        return true;
    }

    private bool Pay(double price)
    {
        if (S.Coin < price) return false;
        S.Coin -= price;
        S.CoinSpent += price;
        return true;
    }

    private void Bought(ShopItemKind k, int index, double price, int level)
    {
        double renown = RenownFor(price);
        S.Renown += renown;
        S.Purchases++;
        if (S.FirstPurchaseTime < 0) S.FirstPurchaseTime = Time;
        Emit(new TownEvent { Kind = TownEventKind.Purchase, Item = k, Index = index, Coin = price, Value = renown, Level = level });
        if ((k == ShopItemKind.Villager || k == ShopItemKind.Pace || k == ShopItemKind.Pave) && level % TownRules.PerkEvery == 0)
            Emit(new TownEvent { Kind = TownEventKind.PerkEarned, Item = k, Level = level, Text = PerkNames[(int)k] });
        CheckLevels();
    }

    // ---- The bell ----

    public double BellRest => TownRules.BellRest * Math.Max(0.5, 1 - TownRules.PacePerkBell * (S.PaceLevel / TownRules.PerkEvery));
    public bool BellReady => S.BellUnlocked && Time >= S.BellReadyAt;
    /// <summary>Seconds until the bell can ring again (0 when ready).</summary>
    public double BellWait => Math.Max(0, S.BellReadyAt - Time);

    /// <summary>Everyone hurries for TownRules.BellDuration seconds; then the bell rests.</summary>
    public bool RingBell()
    {
        if (!BellReady) return false;
        S.BellUntil = Time + TownRules.BellDuration;
        S.BellReadyAt = Time + BellRest;
        S.BellRings++;
        Emit(new TownEvent { Kind = TownEventKind.BellRung, Value = TownRules.BellDuration });
        return true;
    }

    // ---- The next age ----

    public bool CanRingNextAge => S.Charter && !S.NextAgeRung;

    /// <summary>The hook for the second age (not built yet): marks it rung and emits NextAge; the town keeps running as it is.</summary>
    public bool RingNextAge()
    {
        if (!CanRingNextAge) return false;
        S.NextAgeRung = true;
        Emit(new TownEvent { Kind = TownEventKind.NextAge, Level = S.Level, Text = "The next age" });
        return true;
    }
}
