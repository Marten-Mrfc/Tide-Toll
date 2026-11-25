// DIAG: temporary, removed before the step ends.
using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;
namespace TideToll.Sim.Tests;
public class ZzDiagTests
{
    [Fact]
    [Trait("Category", "Diag")]
    public void DiagCapacity()
    {
        var o = new System.Text.StringBuilder();
        foreach (var t in new[] { CrossingType.Drawbridge, CrossingType.Footbridge })
        {
            World.DiagClips = World.DiagClipCrossed = World.DiagTokenOn = World.DiagPassOn = 0;
            var narrow = Rich with { HaulGoods = true, FootbridgeWalkersPerDay = 20, DrawbridgeWalkersPerDay = 20 };
            var a = Make(5, 400, narrow);
            var xa = Build(a, t);
            ToyHarness.Run(a, 1);
            World.DiagClips = World.DiagClipCrossed = World.DiagTokenOn = World.DiagPassOn = 0;
            int c0 = xa.WalkersCrossed;
            for (int i = 0; i < 4 * a.Params.TicksPerDay; i++) a.Tick();
            o.AppendLine($"{RiverTestRules.Trail.PreviewV2} {t}: crossed/day {(xa.WalkersCrossed - c0) / 4.0} clips {World.DiagClips} clipCrossed {World.DiagClipCrossed} tokenOn {World.DiagTokenOn} passOn {World.DiagPassOn}");
        }
        File.AppendAllText("/tmp/claude-0/-home-user-tide-toll/77814b05-b8a7-5fc0-8fa0-f1576cbdb01f/scratchpad/diag.txt", o.ToString());
    }
}
