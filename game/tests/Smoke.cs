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
        Check(Main.Hud.CoinText.EndsWith(" coin"), "coin shown");
        Check(Main.Hud.FloatersStarted > 0, "a +N floated at a delivery");
        Notes.Add($"game_s={ran:0.0} coin={T.CoinEarned:0} trips={T.Trips} uploads={Main.Ground.Uploads}");
    }
}
