using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 4: the second age's screenshots (-- --river-only, or after the rest): the ringing-in moment, the second
/// age's page, the landing with a boat moored at high water, low water at the ford, the ferry mid-river with its
/// queues, the pasture and the weaver, the town on both banks at the seal, the drawbridge lifted for a boat, the book
/// view of the second age. Files town-river-*-WxH.png.
/// </summary>
public partial class Screenshot
{
    private async Task RiverShots()
    {
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60 * 10);
        Check(T.HasCharter, "the bot reached the charter");
        Main.Rig.UserMoved = false;
        await Frames(30);
        await Key(Godot.Key.N);
        var t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < 380) await Frames(1);
        await Shot("river-ring");
        await Until(() => Main.Hud.AgePageOpen, 2000, "the second age's page");
        await Frames(5);
        await Shot("river-age-page");
        Main.Hud.CloseAgePage();
        bot.PlayOn = true;
        var rp = T.RiverPlan!;

        // The landing with a boat moored at high water, cargo coming in.
        Main.Driver.RunBot(bot, t => t.HighWater && Moored(t) >= 0 && t.Boat(Moored(t)).Loaded > 2, 20 * 60 * 60);
        await Look(rp.Px[RiverPoint.Landing], rp.Py[RiverPoint.Landing] + 2, 70);
        await Shot("river-landing");

        // Low water with the ford open (people wading when anyone uses it).
        Main.Driver.RunBot(bot, t => t.CrossingExists(RiverLinkKind.Ford) && t.TideLevel < 0.1, 20 * 60 * 60);
        var ford = rp.Links[rp.LinkOf(RiverLinkKind.Ford)];
        await Look((ford.Px[0] + ford.Px[^1]) / 2, ford.Py[0], 80);
        await Shot("river-ford-low");

        // The pasture and the weaver.
        Main.Driver.RunBot(bot, t => t.RiverPlotGiven(1) && t.RiverPlotGiven(4), 20 * 60 * 60);
        var past = rp.PlotOf(RiverPlotKind.Pasture);
        await Look(past.X + past.W / 2.0, past.Y + past.H / 2.0, 60);
        await Shot("river-pasture");
        var weaver = rp.PlotOf(RiverPlotKind.Weaver);
        await Look(weaver.X + weaver.W / 2.0, weaver.Y + weaver.H / 2.0, 55);
        await Shot("river-weaver");

        // The ferry mid-river with people waiting.
        Main.Driver.RunBot(bot, t => t.Ferry.Built && t.Ferry.Pos > 0.35 && t.Ferry.Pos < 0.65 && t.Ferry.Aboard > 0, 20 * 60 * 60);
        await Look(T.Ferry.X, T.Ferry.Y, 60);
        await Shot("river-ferry");

        // The town on both banks at the seal.
        Main.Driver.RunBot(bot, t => t.HasSeal, 20 * 60 * 60 * 2);
        Check(T.HasSeal, "the bot reached the seal");
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(4);
        await Shot("river-seal");
        await Until(() => Main.Hud.AgePageOpen, 2000, "the seal's page");
        await Frames(5);
        await Shot("river-seal-page");
        Main.Hud.CloseAgePage();

        // The drawbridge lifted for a boat.
        Main.Driver.RunBot(bot, t => t.DrawbridgeAngle > 1.0, 20 * 60 * 60);
        var draw = rp.Links[rp.LinkOf(RiverLinkKind.Drawbridge)];
        await Look((draw.Px[0] + draw.Px[^1]) / 2, draw.Py[0], 60);
        await Shot("river-drawbridge");

        // The book view of the second age.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal, Main.Rig.MaxSize, instant: true);
        await Frames(30);
        await Shot("river-book");
    }

    private static int Moored(Town t)
    {
        for (int i = 0; i < t.BoatCount; i++) if (t.Boat(i).Phase == BoatPhase.Moored) return i;
        return -1;
    }

    /// <summary>The camera close on a map point (cells), held while the drawing catches up; the town keeps still.</summary>
    private async Task Look(double x, double y, float size)
    {
        // The bot's level events reach Main a frame later and turn the follow view back on: let them pass first.
        Main.Driver.SetPaused(true);
        await Frames(4);
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell), size, instant: true);
        await Frames(70);
        Main.Driver.SetPaused(false);
    }
}
