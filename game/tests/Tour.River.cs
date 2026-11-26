using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The tour's second age: rung in by its key, then the bot plays on (PlayOn) between pictures: the landing
/// at high water, the ford, the pasture and the weaver, the ferry, the drawbridge, the river shop and the seal.</summary>
public partial class Tour
{
    private async Task RiverAge()
    {
        // f01, f02: Ring in the next age by its key (N), the page turning, then the second age's page.
        await FollowView();
        await Calm();
        await Key(Godot.Key.N);
        // Last pass: f01 was caught half-way through the wipe. Taken before the wipe, with the ring's banner plainly up;
        // if the wipe has already begun, after it, once it has finished.
        await WaitFor(() => Main.Hud.PageTurning || Main.Hud.BannerText != "", 5000);
        if (Main.Hud.PageTurning) await WaitFor(() => !Main.Hud.PageTurning, 15000);
        await Frames(3);
        await Shot("f01-ring");
        if (!await WaitFor(() => Main.Hud.AgePageOpen, 30000))
        {
            // Seen once: the page did not open by itself after the ring. N ("Read its page again") opens it.
            Wrong("f02-age-page", "The page did not open by itself within 30 s of the ring; opened with N.");
            await Key(Godot.Key.N);
            Check(await WaitFor(() => Main.Hud.AgePageOpen, 10000), "N opens the second age's page");
        }
        await Wait(600);
        await Shot("f02-age-page");
        Main.Hud.CloseAgePage();
        await Frames(3);
        _bot.PlayOn = true;
        var rp = T.RiverPlan!;

        // f03: high water, a boat moored at the landing being loaded; the tide dial in view.
        if (await Early(t => t.HighWater && Moored(t) >= 0 && t.Boat(Moored(t)).Loaded > 2 && t.TideSecondsToTurn > 20, 60))
        {
            await ClosePage();
            await Look(rp.Px[RiverPoint.Landing], rp.Py[RiverPoint.Landing] + 2, 46f);
            await Settle(1000);
            await Calm(8000);
            await Shot("f03-landing");
        }
        else NotMade("f03-landing", "no boat moored with cargo at high water within an hour");

        // f04: the ford: people waiting on the bank at high water (else wading at low water).
        if (await Early(t => t.CrossingExists(RiverLinkKind.Ford) && t.HighWater && t.CrossingWaiting(RiverLinkKind.Ford, 0) + t.CrossingWaiting(RiverLinkKind.Ford, 1) >= 1, 8)
            || await Early(t => t.CrossingExists(RiverLinkKind.Ford) && t.FordOpen && t.CrossingsPerMinute(RiverLinkKind.Ford) > 0, 4))
        {
            var ford = rp.Links[rp.LinkOf(RiverLinkKind.Ford)];
            await ClosePage();
            await Look((ford.Px[0] + ford.Px[^1]) / 2, (ford.Py[0] + ford.Py[^1]) / 2, 40f);
            await Settle(1000);
            await Calm(8000);
            await Shot("f04-ford");
        }
        else NotMade("f04-ford", "the ford was not reached");

        // f05: the pasture on the far bank and the weaver on the near bank, both in one view.
        if (await Early(t => t.RiverPlotGiven(1) && t.RiverPlotGiven(4) && SheepOut(t), 60))
        {
            var past = rp.PlotOf(RiverPlotKind.Pasture); var weaver = rp.PlotOf(RiverPlotKind.Weaver);
            double x0 = Math.Min(past.X, weaver.X), x1 = Math.Max(past.X + past.W, weaver.X + weaver.W);
            double y0 = Math.Min(past.Y, weaver.Y), y1 = Math.Max(past.Y + past.H, weaver.Y + weaver.H);
            float size = (float)Math.Max(44, Math.Max(y1 - y0, (x1 - x0) * 0.56) * TownGround.Cell * 1.5);
            await ClosePage();
            await Look((x0 + x1) / 2, (y0 + y1) / 2, size);
            await Settle(1000);
            await Calm(8000);
            await Shot("f05-pasture");
        }
        else NotMade("f05-pasture", "the pasture and the weaver were not both given");

        // f06: the ferry mid-river with riders, people waiting on both banks.
        if (await Early(t => t.Ferry.Built && t.Ferry.Pos > 0.3 && t.Ferry.Pos < 0.6 && t.Ferry.Aboard > 0 && t.Ferry.WaitingNear + t.Ferry.WaitingFar >= 2, 40)
            || await Early(t => t.Ferry.Built && t.Ferry.Pos > 0.25 && t.Ferry.Pos < 0.75 && t.Ferry.Aboard > 0, 30)
            || await Early(t => t.Ferry.Built && t.Ferry.Aboard > 0, 30))
        {
            await ClosePage();
            // Last pass: f06 showed the footbridge's label. The ferry framed with the raft away from both banks, people
            // waiting, no banner over it.
            await Calm(8000);
            await WaitFor(() => T.Ferry.Pos > 0.25 && T.Ferry.Pos < 0.75 && T.Ferry.WaitingNear + T.Ferry.WaitingFar >= 1 && Main.Hud.BannerText == "", 30000);
            await Look(T.Ferry.X, T.Ferry.Y, 30f);
            await Frames(3);
            await Shot("f06-ferry");
        }
        else NotMade("f06-ferry", "the ferry never carried anyone before the seal (bought by the bot at level 22 or not at all)");

        // f08: the second age's shop, with the river's group.
        await Early(t => t.Level >= 24, 120);
        await ClosePage();
        await FollowView();
        await Settle(800);
        await Calm();
        await Shot("f08-river-shop");

        // f07: the drawbridge lifted for a boat, walkers waiting, the toll paid.
        int draw = -1;
        if (await Early(t => t.CrossingExists(RiverLinkKind.Drawbridge) && t.DrawbridgeAngle > 1.0 && t.CrossingWaiting(RiverLinkKind.Drawbridge, 0) + t.CrossingWaiting(RiverLinkKind.Drawbridge, 1) > 0, 30)
            || await Early(t => t.CrossingExists(RiverLinkKind.Drawbridge) && t.DrawbridgeAngle > 1.0, 240))
        {
            draw = rp.LinkOf(RiverLinkKind.Drawbridge);
            var d = rp.Links[draw];
            await ClosePage();
            await Look((d.Px[0] + d.Px[^1]) / 2, (d.Py[0] + d.Py[^1]) / 2, 40f);
            await Frames(2);
            await Wait(500);
            if (T.Tolls == 0) Wrong("f07-drawbridge", "No toll had been paid yet.");
            if (T.CrossingWaiting(RiverLinkKind.Drawbridge, 0) + T.CrossingWaiting(RiverLinkKind.Drawbridge, 1) == 0) Wrong("f07-drawbridge", "Nobody was waiting at the lifted bridge in this run.");
            await Shot("f07-drawbridge");
        }
        else NotMade("f07-drawbridge", "the drawbridge was not lifted for a boat before the seal");

        // f09, f10: the borough seal: the town on both banks, then the page that closes the age.
        Check(await Fast(t => t.HasSeal, 600), "the bot reached the seal");
        // The seal may have come during an earlier wait: its page then covers the town; Esc closes it (as a player).
        await ClosePage();
        await FollowView();
        await Settle(400);
        await Shot("f09-seal");
        if (!await WaitFor(() => Main.Hud.AgePageOpen, 6000))
        {
            await Key(Godot.Key.N); // "Read its page again"
            Check(await WaitFor(() => Main.Hud.AgePageOpen, 10000), "N opens the seal's page");
        }
        await Wait(600);
        await Shot("f10-seal-page");
        Main.Hud.CloseAgePage();
        await Frames(3);
    }

    /// <summary>Fast, but never past the seal: the river's pictures come before it, in play order.</summary>
    private async Task<bool> Early(Func<Town, bool> cond, double maxMinutes)
    {
        await Fast(t => t.HasSeal || cond(t), maxMinutes);
        return !T.HasSeal && cond(T);
    }

    private async Task ClosePage()
    {
        if (!Main.Hud.AgePageOpen) return;
        await Key(Godot.Key.Escape);
        Check(await WaitFor(() => !Main.Hud.AgePageOpen, 5000), "Esc closes the age page");
    }

    private static int Moored(Town t)
    {
        for (int i = 0; i < t.BoatCount; i++) if (t.Boat(i).Phase == BoatPhase.Moored) return i;
        return -1;
    }

    private static bool SheepOut(Town t) => t.RiverPlotGiven(1);
}
