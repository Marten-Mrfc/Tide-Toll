using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Rules v3: the charter drive (Active while it runs; Done once met).</summary>
public struct DriveView
{
    public bool Active, Done;
    public string Name;            // "The charter drive"
    public Good Good;
    public int Amount, Delivered;
    public double RewardRenown;    // renown in all
    public double RenownGiven;     // renown paid so far (paid in parts as units arrive)
}

/// <summary>Rules v3: the charter drive and extra hands at queued workplaces.</summary>
public sealed partial class Town
{
    public const string DriveName = "The charter drive";

    public DriveView Drive => new()
    {
        Active = S.DriveActive, Done = S.DriveDone, Name = DriveName, Good = S.DriveGood, Amount = S.DriveAmount,
        Delivered = S.DriveDelivered, RewardRenown = S.DriveRenown, RenownGiven = S.DriveRenownGiven,
    };

    /// <summary>"The charter drive: send 400 Flour for the charter (120 sent)", or "" when no drive runs.</summary>
    public string DriveText => S.DriveActive ? $"{DriveName}: send {S.DriveAmount} {GoodNames[(int)S.DriveGood]} for the charter ({S.DriveDelivered} sent)" : "";

    private void StartDrive()
    {
        bool market = _marketPlot >= 0 && PlotWorks(_marketPlot);
        Good g = Capacity(Good.Flour) > 0 && market ? Good.Flour : Capacity(Good.Grain) > 0 ? Good.Grain : Good.Water;
        double unit = UnitValue(g);
        double secs = TownRules.DriveMinutes * 60;
        double amount = Math.Max(30, CoinPerMinute) * TownRules.DriveMinutes / unit;
        amount = Math.Min(amount, TownRules.DriveCapacityShare * Capacity(g) * secs);
        double need = LevelRenownOf(Plan.MaxLevel) - S.Renown;
        S.DriveActive = true; S.DriveGood = g; S.DriveAmount = Math.Max(10, (int)Math.Round(amount)); S.DriveDelivered = 0;
        S.DriveRenown = Math.Max(1, Math.Round(TownRules.DriveShare * Math.Max(0, need)));
        S.DriveRenownGiven = 0; S.DriveStart = Time;
        Emit(new TownEvent { Kind = TownEventKind.DriveAsked, Good = g, Amount = S.DriveAmount, Value = S.DriveRenown });
    }

    private void DriveDelivered(int units)
    {
        int add = Math.Min(units, S.DriveAmount - S.DriveDelivered);
        if (add <= 0) return;
        S.DriveDelivered += add;
        bool done = S.DriveDelivered >= S.DriveAmount;
        double renown = done ? S.DriveRenown - S.DriveRenownGiven : Math.Floor(S.DriveRenown * S.DriveDelivered / S.DriveAmount) - S.DriveRenownGiven;
        S.DriveRenownGiven += renown;
        S.Renown += renown;
        Emit(new TownEvent { Kind = TownEventKind.DriveProgress, Good = S.DriveGood, Amount = S.DriveDelivered, Value = renown });
        if (done)
        {
            S.DriveActive = false; S.DriveDone = true; S.DriveEnd = Time;
            Emit(new TownEvent { Kind = TownEventKind.DriveMet, Good = S.DriveGood, Amount = S.DriveAmount, Value = S.DriveRenown });
        }
        CheckLevels();
    }

    // ---- Extra hands ----

    private static readonly string[] Ordinals = { "", "", "A second", "A third", "A fourth", "A fifth", "A sixth", "A seventh" };
    private static readonly string[] HandPlaces = { " hand at the well", " hand at the field", " hand at the mill" };
    private static readonly string[,] HandNames = BuildHandNames();

    private static string[,] BuildHandNames()
    {
        var a = new string[3, Ordinals.Length];
        for (int k = 0; k < 3; k++) for (int o = 0; o < Ordinals.Length; o++) a[k, o] = Ordinals[Math.Max(2, o)] + HandPlaces[k];
        return a;
    }

    private static int HandKind(PlotKind k) => k == PlotKind.Well ? 0 : k == PlotKind.Field ? 1 : 2;

    public int HandsBought(int workplace) => S.Workplaces[workplace].Hands;
    public bool HandOffered(int workplace) => S.Workplaces[workplace].HandOffered;

    /// <summary>"A third hand at the mill": the offer's name at a workplace (the place it would add).</summary>
    public string HandName(int workplace)
    {
        var w = S.Workplaces[workplace];
        return HandNames[HandKind(Plan.Plots[w.Plot].Kind), Math.Min(w.Places + 1, Ordinals.Length - 1)];
    }

    public double HandPrice(int workplace)
    {
        var w = S.Workplaces[workplace];
        var k = Plan.Plots[w.Plot].Kind;
        double b = k == PlotKind.Well ? TownRules.HandWellBase : k == PlotKind.Field ? TownRules.HandFieldBase : TownRules.HandMillBase;
        return Math.Ceiling(b * Math.Pow(TownRules.HandGrowth, w.Hands));
    }

    private void StepHands()
    {
        var ws = S.Workplaces;
        for (int i = 0; i < ws.Length; i++)
        {
            var w = ws[i];
            if (!w.Open || w.QueueLen < TownRules.HandQueueLength) { w.QueueSince = -1; continue; }
            if (w.QueueSince < 0) { w.QueueSince = Time; continue; }
            if (!w.HandOffered && w.Hands < TownRules.HandMax && Time - w.QueueSince >= TownRules.HandQueueSeconds)
            {
                w.HandOffered = true;
                Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Hand, Index = i, Plot = w.Plot, Text = HandName(i) });
            }
        }
    }

    private void AddHandItems(List<ShopItem> into)
    {
        for (int i = 0; i < S.Workplaces.Length; i++)
            if (S.Workplaces[i].HandOffered) into.Add(Item(ShopItemKind.Hand, i, HandName(i), S.Workplaces[i].Hands, HandPrice(i), ""));
    }

    /// <summary>Buys the extra hand on offer at a workplace: one more place there. False when none is on offer.</summary>
    public bool BuyHand(int workplace)
    {
        if (workplace < 0 || workplace >= S.Workplaces.Length || !S.Workplaces[workplace].HandOffered) return false;
        double p = HandPrice(workplace);
        if (!Pay(p)) return false;
        var w = S.Workplaces[workplace];
        w.Hands++; w.HandOffered = false; w.QueueSince = -1;
        RefreshWorkplaces();
        Bought(ShopItemKind.Hand, workplace, p, w.Hands);
        return true;
    }
}
