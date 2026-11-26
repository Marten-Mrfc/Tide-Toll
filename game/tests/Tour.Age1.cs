using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The tour's first age: the first minute, upgrades, levels, a road wearing in and paved, the charter.</summary>
public partial class Tour
{
    private int _road = -1;            // the level-2 lane followed from a faint trail to cobbles
    private Vector3 _roadPivot; private float _roadSize = 34f;

    private async Task FirstAge()
    {
        // a01: the first minute, close on the cottage, the well and the trail between them.
        await LookRoad(0, 36f);
        Main.Driver.Advance(25);
        await Settle();
        await Calm();
        await Shot("a01-first-minute");

        // a02: the first delivery: wait (normal speed) for the coin to rise over the cottage.
        int f0 = Main.Hud.FloatersStarted;
        if (await WaitFor(() => Main.Hud.FloatersStarted > f0, 120000)) { await Wait(350); await Shot("a02-first-coin"); }
        else NotMade("a02-first-coin", "no delivery floater within two minutes of real time");

        // a03: the whole interface at level 1, in the normal follow view.
        await FollowView();
        await Settle(800);
        await Shot("a03-interface");

        // b01: a second villager bought through its bookmark (a real click), walking the trail.
        await Idle(t => Main.Hud.Mark(ShopItemKind.Villager) is { Disabled: false } || t.Coin >= 40, 5);
        await Frames(3);
        var vm = Main.Hud.Mark(ShopItemKind.Villager);
        long p0 = T.Purchases;
        if (vm != null && !vm.Disabled) await Click(TestInput.Centre(vm));
        Check(T.Purchases == p0 + 1 && T.VillagerCount >= 2, "b01: the villager bookmark bought a villager");
        await LookRoad(0, 36f);
        await Settle(2500);
        await Calm();
        await Shot("b01-villager");

        // c01: level 2 reached: the banner names the gift; the new cottage and lane appear.
        await Fast(t => t.Level >= 2, 20);
        await FollowView();
        await Settle(300);
        if (Main.Hud.BannerText.Length > 0) await Shot("c01-level-banner");
        else { NotMade("c01-level-banner", "the level banner had gone before the drawing caught up"); }

        // d01: the lane just given, a faint trail.
        var lv2 = T.Plan.Level(2);
        _road = lv2.Roads.Length > 0 ? lv2.Roads[0] : 0;
        await LookRoad(_road, _roadSize);
        _roadPivot = Main.Rig.PivotLocal;
        await Settle();
        await Calm();
        await Shot("d01-trail-new");

        // b07: the bell (unlocked at level 2), rung by its key.
        await Idle(t => t.BellReady, 2);
        await FollowView();
        await Calm();
        if (T.BellReady)
        {
            await Key(Godot.Key.Space);
            await Wait(1200);
            Check(T.BellRinging, "b07: Space rang the bell");
            await Shot("b07-bell");
        }
        else NotMade("b07-bell", "the bell was not ready");
        _bot.NoBell = false;

        // c02: staked plots: the next level's plot, staked and labelled.
        await StakedPlots();

        // b02 and c03: level 3, the field; Pace and the Basket bought by their bookmarks.
        await Fast(t => t.Level >= 3, 20);
        await BuyByMark(ShopItemKind.Load, 8);
        await BuyByMark(ShopItemKind.Pace, 8);
        await Idle(t => Carrier(t, Good.None) >= 0, 1);
        int v = Carrier(T, Good.None);
        if (v >= 0) { var vv = T.Villager(v); await Look(vv.X, vv.Y, 16f); }
        await Settle(1200);
        await Calm();
        await Shot("b02-pace-and-load");

        int field = -1;
        for (int p = 0; p < T.Plan.Plots.Length; p++) if (T.Plan.Plots[p].Kind == PlotKind.Field && T.PlotGiven(p)) { field = p; break; }
        await Idle(t => Carrier(t, Good.Grain) >= 0, 3);
        if (field >= 0 && Carrier(T, Good.Grain) >= 0)
        {
            await LookPlot(field, 46f);
            await Settle(2500);
            await Calm();
            await Shot("c03-field");
        }
        else NotMade("c03-field", "no field or nobody carrying Grain within three minutes of level 3");

        // c05: a request running, its card in the normal view.
        if (await Fast(t => t.Request.Active && t.Request.Delivered > 0 && t.Request.SecondsLeft < t.Request.Seconds * 0.7, 20))
        {
            await FollowView();
            await Settle(800);
            await Calm(6000);
            await Shot("c05-request");
        }
        else NotMade("c05-request", "no request with deliveries within 20 minutes");

        // c04: the choice of two at level 4 (the simulation offers it; is it drawn?).
        if (await Fast(t => t.ChoiceOpen, 30))
        {
            await FollowView();
            await Settle(800);
            string a = T.Choice.AName, b = T.Choice.BName;
            if (ShowsText(a) || ShowsText(b)) await Shot("c04-choice");
            else NotMade("c04-choice", $"the simulation offers a choice of two ({a} or {b}) but the game draws nothing for it in this build");
        }
        else NotMade("c04-choice", "no choice was offered by level " + T.Level);

        await StallAndExtend();
        await RoadsAndCrowds();
        await Later();
    }

    private async Task StakedPlots()
    {
        int next = -1;
        for (int lv = T.Level + 1; lv <= T.MaxLevel && next < 0; lv++)
            foreach (var p in T.Plan.Level(lv).Plots) { next = p; break; }
        if (next < 0) { NotMade("c02-staked-plots", "no later plot in the plan"); return; }
        await LookPlot(next, 70f);
        await Settle(800);
        await Calm();
        await Shot("c02-staked-plots");
    }

    private async Task BuyByMark(ShopItemKind kind, double maxMinutes)
    {
        await Idle(t => Main.Hud.Mark(kind) is { Disabled: false }, maxMinutes);
        await Frames(3);
        var m = Main.Hud.Mark(kind);
        if (m == null || m.Disabled) { Wrong(kind == ShopItemKind.Load ? "b02-pace-and-load" : "b02-pace-and-load", $"{kind} could not be bought within {maxMinutes} minutes."); return; }
        long p0 = T.Purchases;
        await Click(TestInput.Centre(m));
        Check(T.Purchases == p0 + 1, $"the {kind} bookmark bought it");
    }

    /// <summary>A walking villager carrying the good (None: anything), or -1.</summary>
    private static int Carrier(Town t, Good g)
    {
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            if (v.Phase == VillagerPhase.Walking && v.Carry != Good.None && (g == Good.None || v.Carry == g) && v.Amount > 1) return i;
        }
        return -1;
    }

    private async Task StallAndExtend()
    {
        // b06: the market stall bought by its bookmark, standing on its plot.
        int stall = -1;
        for (int o = 0; o < T.Plan.Offers.Length; o++) if (T.Plan.Offers[o].Kind == OfferKind.MarketStall) stall = o;
        bool bot0 = _bot.NoPave;
        if (stall >= 0 && await Fast(t => t.OfferAvailable(stall) || t.OfferBought(stall), 40) && !T.OfferBought(stall))
        {
            await Idle(t => Main.Hud.Mark(ShopItemKind.Building, stall) is { Disabled: false }, 10);
            await Frames(3);
            var m = Main.Hud.Mark(ShopItemKind.Building, stall);
            if (m != null && !m.Disabled) await Click(TestInput.Centre(m));
        }
        if (stall >= 0 && T.OfferBought(stall))
        {
            await LookPlot(T.Plan.Offers[stall].Plot, 40f);
            await Settle(1500);
            await Calm();
            await Shot("b06-building");
        }
        else NotMade("b06-building", "the market stall was not bought");

        // b04 and b05: Extend mode by its bookmark, a cottage pointed at; then the same cottage after two extensions.
        if (!await Fast(t => t.Level >= 5, 30)) { NotMade("b04-extend-mode", "level 5 not reached"); NotMade("b05-extended", "level 5 not reached"); return; }
        int cot = -1;
        if (!await Idle(t => { cot = PlainCottage(t); return cot >= 0 && t.CanExtendNow(cot) && t.Coin >= t.ExtendPrice * 3; }, 10))
            await Fast(t => { cot = PlainCottage(t); return cot >= 0 && t.CanExtendNow(cot); }, 30);
        cot = PlainCottage(T);
        if (cot >= 0 && !T.CanExtendNow(cot)) cot = -1;
        if (cot < 0) { NotMade("b04-extend-mode", "no plain cottage to extend"); NotMade("b05-extended", "no plain cottage"); return; }
        await LookPlot(cot, 30f);
        await Settle(1000);
        await Calm();
        var em = Main.Hud.Mark(ShopItemKind.Extend);
        Check(em != null, "an Extend bookmark");
        await Click(TestInput.Centre(em!));
        TestInput.Motion(Main.Hud.PlotScreen(cot));
        await Frames(4);
        await Wait(800);
        Check(Main.Hud.ExtendMode, "b04: the Extend bookmark started Extend mode");
        await Shot("b04-extend-mode");
        // The first extension by a click; the second when the town's level puts another step on sale (the bot plays
        // on meanwhile; it may take that step on this cottage itself).
        TestInput.Motion(Main.Hud.PlotScreen(cot));
        await Frames(3);
        await Click(Main.Hud.PlotScreen(cot));
        if (Main.Hud.ExtendMode) await Key(Godot.Key.Escape);
        Check(T.Extensions(cot) == 1, "b04: the click extended the cottage");
        await Fast(t => t.CanExtendNow(cot) || t.Extensions(cot) >= 2, 60);
        for (int tries = 0; tries < 3 && T.Extensions(cot) < 2; tries++)
        {
            if (!T.CanExtendNow(cot)) await Fast(t => t.CanExtendNow(cot) || t.Extensions(cot) >= 2, 30);
            if (T.Extensions(cot) >= 2) break;
            await LookPlot(cot, 30f);
            await Frames(2);
            if (!Main.Hud.ExtendMode) await Click(TestInput.Centre(Main.Hud.Mark(ShopItemKind.Extend)!));
            TestInput.Motion(Main.Hud.PlotScreen(cot));
            await Frames(4);
            if (Main.Hud.HoverCottage == cot) await Click(Main.Hud.PlotScreen(cot));
            if (Main.Hud.ExtendMode) await Key(Godot.Key.Escape);
        }
        await LookPlot(cot, 30f);
        if (Main.Hud.ExtendMode) await Key(Godot.Key.Escape);
        TestInput.Motion(new Vector2(960, 1070));
        await Settle(1500);
        await Calm();
        if (T.Extensions(cot) == 2) await Shot("b05-extended");
        else NotMade("b05-extended", $"the cottage has {T.Extensions(cot)} extensions after two clicks");
        _bot.NoPave = bot0;
    }

    private static int PlainCottage(Town t)
    {
        for (int p = 0; p < t.Plan.Plots.Length; p++)
            if (t.Plan.Plots[p].Kind == PlotKind.Cottage && t.PlotGiven(p) && t.Extensions(p) == 0 && t.CanExtend(p, out _)) return p;
        return -1;
    }
}
