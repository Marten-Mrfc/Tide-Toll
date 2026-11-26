using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-smoke: the town scene loads with all its pieces and runs 30 game seconds (at 4x) through its own frame loop;
/// the driver keeps the tick rate, the villager is drawn, coin is earned and the ground has been uploaded. Any error
/// line in the log fails the step (tools/godot-test.sh).
/// </summary>
public partial class Smoke : TownTestBase
{
    protected override string Marker => $"SMOKE PASS renderer={RenderingServer.GetCurrentRenderingMethod()}";

    protected override async Task Run()
    {
        string[] paths =
        {
            "Driver", "CameraRig", "Audio", "Hud", "Book/LeftPage/Paper", "Book/RightPage/MapOrigin/Ground",
            "Book/RightPage/MapOrigin/Buildings", "Book/RightPage/MapOrigin/Trees", "Book/RightPage/MapOrigin/Villagers",
        };
        foreach (var p in paths) Check(Main.GetNodeOrNull(p) != null, "missing node " + p);
        Check(T.Level == 1 && T.VillagerCount == 1, "a new town starts at level 1 with one villager");
        Check(Main.Buildings.BuiltCount >= 2, "the cottage and the well are drawn at the start");
        long steps0 = Main.Driver.StepsRun;
        double t0 = T.Time;
        Main.Driver.SetSpeed(4);
        await Until(() => T.Time - t0 >= 30, 60 * 30, "30 game seconds");
        double ran = T.Time - t0;
        Check(Main.Driver.StepsRun - steps0 == (long)System.Math.Round(ran * TownRules.TickHz), "every tick counted once");
        Check(T.CoinEarned > 0, "coin earned in the first 30 seconds");
        Check(Main.Villagers.Drawn == T.VillagerCount, "every villager drawn");
        Check(Main.Ground.Uploads > 5, "the ground was uploaded");
        // Night 3: the purse shows the amount beside the coin sign (an icon), never the word "coin" (layout sheet section 4).
        Check(Main.Hud.CoinText == TownHud.Money(System.Math.Floor(T.Coin)), $"coin shown ({Main.Hud.CoinText})");
        Check(Main.Hud.FloatersStarted > 0, "a +N floated at a delivery");
        Notes.Add($"game_s={ran:0.0} coin={T.CoinEarned:0} trips={T.Trips} uploads={Main.Ground.Uploads}");
        SlowFramesKeepGameTime();
        LogsAreCapped();
    }

    /// <summary>
    /// Round 4 (r4-review A10): a slow frame at high speed loses no game time: a 0.6 s frame at 64x owes 384 steps,
    /// more than one frame may run; the rest run in the next frames.
    /// </summary>
    private void SlowFramesKeepGameTime()
    {
        var d = new TownDriver();
        d.Reset(5);
        d.SetSpeed(64);
        d._Process(0.6);
        Check(d.StepsRun == d.MaxStepsPerFrame, $"a slow frame ran {d.StepsRun} steps, not the cap {d.MaxStepsPerFrame}");
        for (int i = 0; i < 5; i++) d._Process(0);
        long want = (long)System.Math.Round(0.6 * 64 * TownRules.TickHz);
        Check(System.Math.Abs(d.StepsRun - want) <= 1, $"a 0.6 s frame at 64x ran {d.StepsRun} steps in all, not {want}");
        d.Free();
        Notes.Add($"slow-frame={want}steps");
    }

    /// <summary>Round 4 (A10): the note and banner logs keep their last lines only.</summary>
    private void LogsAreCapped()
    {
        for (int i = 0; i < TownHud.LogCap + 50; i++) { Main.Hud.Note("Smoke note " + i); Main.Hud.Banner("Level", "smoke " + i); }
        Check(Main.Hud.NoteLog.Count <= TownHud.LogCap && Main.Hud.BannerLog.Count <= TownHud.LogCap,
            $"the logs grow without limit (notes {Main.Hud.NoteLog.Count}, banners {Main.Hud.BannerLog.Count})");
        Check(Main.Hud.BannerLog[^1] == "Level: smoke " + (TownHud.LogCap + 49), "the banner log lost its newest line");
    }
}
