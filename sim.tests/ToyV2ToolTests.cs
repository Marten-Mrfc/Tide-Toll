using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// Gravel and gates (toy rounds 3 and 4) on SimParams.Toy: the edits-at-fixed-ticks trace of the finished toy replayed
/// (fence, gravel with the brush's snap, gates on fences at 0, 30, 45, 60 and 90 degrees, the three refusals, a bridge,
/// erase of half a two-cell gate, gravel and the bridge) with every hash compared each 50 ticks; and the toy's own gate
/// and gravel checks (test/rules.test.ts, test/sim.test.ts) ported one for one.
/// </summary>
public class ToyV2ToolTests
{
    private readonly ITestOutputHelper _out;
    public ToyV2ToolTests(ITestOutputHelper output) => _out = output;
    private static readonly SimParams Toy = SimParams.Toy;
    private const int N = 128;

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private static void SameTarget(JsonElement g, GateTarget t, string what)
    {
        Assert.True(Ints(g.GetProperty("cells")).SequenceEqual(t.Cells), $"{what}: cells [{string.Join(",", t.Cells)}], toy [{string.Join(",", Ints(g.GetProperty("cells")))}]");
        Assert.Equal(g.GetProperty("remove").GetBoolean(), t.Remove);
        Assert.Equal(g.GetProperty("reason").GetString(), t.Reason);
    }

    /// <summary>The toy's edits at fixed ticks (rain and growth off), replayed with the toy's cells; every outcome and hash compared.</summary>
    [Fact]
    public void EditsAtFixedTicksMatchTheToy()
    {
        var g = Golden2.Root.GetProperty("edits")[0];
        Assert.False(g.GetProperty("opts").GetProperty("rain").GetBoolean());
        ReplayEdits(g, Toy);
    }

    /// <summary>
    /// Replays one edits-at-fixed-ticks record of the comparison data on <paramref name="p"/>: every tool outcome, the
    /// forced rain and its stop, and every hash each 50 ticks (positions drift by an ulp and are not compared).
    /// </summary>
    internal static void ReplayEdits(JsonElement g, SimParams p)
    {
        var w = new World(g.GetProperty("seed").GetInt32(), g.GetProperty("walkers").GetInt32(), p);
        var log = g.GetProperty("log").EnumerateArray().ToArray();
        var rows = g.GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetDouble()).ToArray()).ToArray();
        int li = 0, ri = 0;
        var gates = new List<int[]>();
        for (int t = 1; t <= 1500; t++)
        {
            w.Tick();
            while (li < log.Length && log[li].GetProperty("tick").GetInt32() == t)
            {
                var e = log[li++];
                string op = e.GetProperty("op").GetString()!;
                switch (op)
                {
                    case "fence":
                        Assert.Equal(e.GetProperty("n").GetInt32(), w.PlaceFence(Ints(e.GetProperty("cells"))));
                        break;
                    case "gravel":
                        foreach (var s in e.GetProperty("snaps").EnumerateArray())
                            Assert.True(s[1].GetInt32() == w.GravelSnap(s[0].GetInt32()), $"gravel snap of cell {s[0].GetInt32()}: {w.GravelSnap(s[0].GetInt32())}, toy {s[1].GetInt32()}");
                        Assert.Equal(e.GetProperty("n").GetInt32(), w.PlaceGravel(Ints(e.GetProperty("cells"))));
                        break;
                    case "gate":
                        Assert.False(e.TryGetProperty("skipped", out _), "the toy found no place for a gate at this angle");
                        var pt = e.GetProperty("point");
                        double px = pt[0].GetDouble(), py = pt[1].GetDouble();
                        Assert.Equal(e.GetProperty("n").GetInt32(), w.PlaceFence(Ints(e.GetProperty("fence"))));
                        var target = w.GateTarget(px, py);
                        SameTarget(e.GetProperty("target"), target, $"gate at {e.GetProperty("angle").GetInt32()} degrees");
                        gates.Add(target.Cells);
                        bool placed = target.Cells.Length > 0 && !target.Remove && w.PlaceGate(target.Cells[0]);
                        Assert.Equal(e.GetProperty("placed").GetBoolean(), placed);
                        SameTarget(e.GetProperty("again"), w.GateTarget(px, py), $"the gate at {e.GetProperty("angle").GetInt32()} degrees, clicked again");
                        break;
                    case "refusals":
                        SameTarget(e.GetProperty("far"), w.GateTarget(2.5, 2.5), "no fence near");
                        int post = e.GetProperty("post").GetInt32();
                        w.PlaceFence(new[] { post });
                        SameTarget(e.GetProperty("lone"), w.GateTarget(post % N + 0.5, post / N + 0.5), "a lone post");
                        int[] wall = Ints(e.GetProperty("wall"));
                        w.PlaceFence(wall);
                        int mid = wall[wall.Length / 2];
                        SameTarget(e.GetProperty("noWay"), w.GateTarget(mid % N + 0.5, mid / N + 0.5), "a fence along a building's wall");
                        break;
                    case "bridge":
                        int[] cells = Ints(e.GetProperty("cells"));
                        int row = cells[0] / N;
                        var b = w.PlaceBridge(w.RiverX[row], row);
                        Assert.NotNull(b);
                        Assert.Equal(cells, b!.Cells);
                        break;
                    case "erase":
                        int[] er = Ints(e.GetProperty("cells"));
                        Assert.Equal(e.GetProperty("n").GetInt32(), w.Erase(er));
                        // Half of a two-cell gate erased: its partner stays a gate on its own.
                        var pair = gates.FirstOrDefault(x => x.Length == 2) ?? gates.FirstOrDefault() ?? Array.Empty<int>();
                        if (pair.Length == 2)
                        {
                            Assert.Equal(e.GetProperty("pairPartnerGate").GetInt32(), w.Gate[pair[1]]);
                            Assert.Equal(e.GetProperty("pairPartnerPair").GetInt32(), w.GatePair[pair[1]]);
                        }
                        break;
                    case "startRain":
                    case "stopRain":
                        if (op == "startRain") w.StartRain(); else w.StopRain(dry: true);
                        var x = w.Weather;
                        double[] wx = { x.Rain, x.Wetness, x.Raining ? 1 : 0, x.RainStart, x.RainEnd, x.MudUntil, x.RainDays };
                        if (w.RainOn)
                            Assert.True(e.GetProperty("weather").EnumerateArray().Select(Golden2.D).SequenceEqual(wx), $"tick {t}: weather after {op} [{string.Join(",", wx)}]");
                        Assert.Equal(e.GetProperty("mudCells").GetInt32(), w.MudCells);
                        break;
                    default:
                        Assert.Fail($"unknown op {op}");
                        break;
                }
            }
            if (t % 50 == 0)
            {
                var r = rows[ri++];
                Assert.Equal(t, (int)r[0]);
                // columns: tick, wear, cost, costX, gravel, gate, gatePair, fence, mud, version, flow, pos (positions drift by an ulp: not compared)
                var now = new double[] { Fnv(w.Wear), Fnv(w.Cost), Fnv(w.CostX), Fnv(w.Gravel), Fnv(w.Gate), Fnv(w.GatePair), Fnv(w.Fence), Fnv(w.Mud), w.Version, Fnv(w.Flow) };
                string[] names = { "wear", "cost", "costX", "gravel", "gate", "gatePair", "fence", "mud", "version", "flow" };
                for (int k = 0; k < now.Length; k++)
                    Assert.True(r[k + 1] == now[k], $"tick {t}: {names[k]} differs from the toy");
            }
        }
        Assert.Equal(log.Length, li);
        Assert.Equal(rows.Length, ri);
    }

    /// <summary>After the erase at tick 900 of the edits trace: the partner of the erased gate cell is a one-cell gate.</summary>
    [Fact]
    public void ErasingHalfATwoCellGateLeavesItsPartnerAGateOnItsOwn()
    {
        var w = new World(1, 50, Toy);
        var (ox, oy) = OpenPatch(w);
        var stair = Line(w, ox, oy, ox + 9, oy + 9);
        w.PlaceFence(stair.ToArray());
        int pairAt = stair.Skip(2).Take(stair.Count - 4).First(c => w.GateCellsFor(c)?.Length == 2);
        var cells = w.GateCellsFor(pairAt)!;
        Assert.True(w.PlaceGate(pairAt));
        Assert.Equal(1, w.Erase(new[] { cells[0] }));
        Assert.Equal(0, w.Fence[cells[0]]);
        Assert.Equal(0, w.Gate[cells[0]]);
        Assert.Equal(1, w.Gate[cells[1]]);
        Assert.Equal(0, w.GatePair[cells[1]]);
        Assert.Equal(1, w.Fence[cells[1]]);
    }

    // ---------------------------------------------------------------- the toy's checks (test/rules.test.ts, test/sim.test.ts)

    private static List<int> Line(World w, int x0, int y0, int x1, int y1)
    {
        var l = new List<int>();
        w.Grid.Line4(x0, y0, x1, y1, l);
        return l;
    }

    /// <summary>An open patch of meadow away from buildings and the river (gateRefusalTests).</summary>
    private static (int, int) OpenPatch(World w)
    {
        for (int y = 10; y < N - 20; y += 3)
            for (int x = 6; x < N - 20; x += 3)
            {
                bool ok = true;
                for (int yy = y - 3; yy < y + 12 && ok; yy++) for (int xx = x - 3; xx < x + 16 && ok; xx++) ok = w.CanFence(yy * N + xx) && w.Land[yy * N + xx] == 1;
                if (ok) return (x, y);
            }
        throw new InvalidOperationException("no open patch");
    }

    [Fact]
    public void GateRefusalsAndSnappingAsTheToy()
    {
        var w = new World(1, 50, Toy);
        var (ox, oy) = OpenPatch(w);
        int I(int x, int y) => y * N + x;
        // A lone post.
        w.PlaceFence(new[] { I(ox, oy) });
        var lone = w.GateTarget(ox + 0.5, oy + 0.5);
        Assert.True(lone.Cells.Length == 0 && lone.Reason == FenceGates.LonePost, $"gate: refused on a lone post (\"{lone.Reason}\")");
        w.Erase(new[] { I(ox, oy) });
        // A fence three cells thick: no way through.
        var block = new List<int>();
        for (int y = oy; y < oy + 3; y++) for (int x = ox; x < ox + 12; x++) block.Add(I(x, y));
        w.PlaceFence(block.ToArray());
        var thick = w.GateTarget(ox + 6.5, oy + 1.5);
        Assert.True(thick.Cells.Length == 0 && thick.Reason == FenceGates.NoWay, $"gate: refused where both sides are blocked (\"{thick.Reason}\")");
        w.Erase(block.ToArray());
        // A straight fence: a click 2 cells off snaps onto it, 3 cells off finds no fence.
        var line = Line(w, ox, oy + 5, ox + 12, oy + 5);
        w.PlaceFence(line.ToArray());
        var near = w.GateTarget(ox + 6.5, oy + 3.5); var far = w.GateTarget(ox + 6.5, oy + 2.5);
        Assert.True(near.Cells.Length == 1 && near.Cells[0] == I(ox + 6, oy + 5), "gate: a click 2 cells from a fence snaps onto it");
        Assert.True(far.Cells.Length == 0 && far.Reason == FenceGates.NoFence, $"gate: a click 3 cells from any fence says so (\"{far.Reason}\")");
        // A diagonal staircase: every cell of it but the ends takes a gate (one cell or a corner pair).
        w.Erase(line.ToArray());
        var stair = Line(w, ox, oy, ox + 9, oy + 9);
        w.PlaceFence(stair.ToArray());
        int takes = stair.Skip(2).Take(stair.Count - 4).Count(c => w.GateCellsFor(c) != null);
        Assert.True(takes == stair.Count - 4, $"gate: every inner cell of a 45-degree fence takes a gate ({takes} of {stair.Count - 4})");
        // Removing one cell of a two-cell gate removes both.
        int pairAt = stair.Skip(2).Take(stair.Count - 4).FirstOrDefault(c => w.GateCellsFor(c)?.Length == 2, -1);
        Assert.True(pairAt >= 0, "gate: a 45-degree fence has a stair corner that takes a two-cell gate");
        var cells = w.GateCellsFor(pairAt)!;
        w.PlaceGate(pairAt);
        bool both = cells.All(c => w.Gate[c] == 1 && w.GatePair[c] == 1);
        // Each cell of a pair has half the delay: its cost is 0.75 above the same cell as a lone gate.
        Assert.Equal(w.CellCost(cells[0], CostOverride.Gate) - Toy.GateCost / 2, w.CellCost(cells[0]), 9);
        w.RemoveGate(cells[1]);
        Assert.True(both && cells.All(c => w.Gate[c] == 0 && w.GatePair[c] == 0 && w.Fence[c] == 1), "gate: a stair-corner gate is two cells and goes as one");
    }

    [Fact]
    public void AFenceLaidOverGravelTakesTheGravelUp()
    {
        var w = new World(2, 50, Toy);
        var cells = Line(w, 20, 20, 30, 20).Where(w.CanGravel).ToArray();
        w.PlaceGravel(cells);
        int n = w.PlaceFence(cells.Take(5).ToArray());
        Assert.True(n == 5 && cells.Take(5).All(c => w.Fence[c] == 1 && w.Gravel[c] == 0) && cells.Skip(5).All(c => w.Gravel[c] == 1), "gravel: a fence laid over gravel takes the gravel up");
    }

    [Fact]
    public void WornGroundUnderGravelDoesNotGrowBackBelowTrailAndErasingLeavesTheTrail()
    {
        var w = new World(1, 50, Toy);
        int g = -1, h = -1;
        for (int c = 0; c < N * N && (g < 0 || h < 0); c++)
        {
            int x = c % N, y = c / N;
            if (w.Land[c] == 0 || x < 3 || x > 6 || y < 3 || y > 124 || DoorDistance(w, c) < 20) continue;
            if (g < 0) g = c; else if (Math.Abs(c - g) > 2 * N) h = c;
        }
        w.PlaceGravel(new[] { g });
        w.SetWearForTests(g, 60); w.SetWearForTests(h, 60);
        Run(w, 20);
        Assert.True(w.Wear[g] >= 25 && w.Wear[h] < 25, $"gravel: worn ground under it does not grow back below trail (wear {w.Wear[g]:F0} under gravel, {w.Wear[h]:F0} beside it after 20 quiet days)");
        Assert.True(w.Erase(new[] { g }) == 1 && w.Gravel[g] == 0 && w.Wear[g] >= 25, "gravel: erasing it leaves the trail beneath");
    }

    private static double DoorDistance(World w, int c)
    {
        double best = double.PositiveInfinity;
        foreach (var b in w.Buildings) best = Math.Min(best, JsMath.Hypot(b.Door % N - c % N, b.Door / N - c / N));
        return best;
    }

    /// <summary>
    /// The toy's gate-angle check: a closed square fence round the well, turned by 0, 30, 45, 60 and 90 degrees, with one
    /// gate clicked on the side facing the houses; 3 days later walkers pass the gate (20+ footsteps in 2 days), none
    /// cross the rest of the fence, and the well is still used (10+ at its door).
    /// </summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void AGateOnAFenceAtAnyAngleCarriesWalkersToTheWell()
    {
        var bad = new List<string>();
        foreach (int deg in new[] { 0, 30, 45, 60, 90 })
        {
            var r = RingWithGate(deg);
            if (r == null) { bad.Add($"gate at {deg} degrees: room for a ring"); continue; }
            var (w, seed, ring, target, placed, well) = r.Value;
            Run(w, 3);
            var fw = w.Metrics.Window().ToArray();
            long through = target.Cells.Sum(c => (long)fw[c]);
            long other = ring.Where(c => w.Gate[c] == 0).Sum(c => (long)fw[c]);
            long atWell = fw[well.Door];
            string line = $"{deg,2} degrees (seed {seed}): ring of {ring.Count} cells; gate of {target.Cells.Length} cell(s); footsteps in 2 days: through the gate {through}, on the rest of the fence {other}, at the well's door {atWell}";
            _out.WriteLine(line);
            if (!(placed && through >= 20 && other == 0 && atWell >= 10)) bad.Add(line);
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    private static (World W, int Seed, List<int> Ring, GateTarget Target, bool Placed, Building Well)? RingWithGate(int deg)
    {
        double th = deg * Math.PI / 180, ux = Math.Cos(th), uy = Math.Sin(th), vx = -uy, vy = ux;
        const int R = 6;
        for (int seed = 1; seed <= 40; seed++)
        {
            var w = new World(seed, 200, Toy);
            var well = w.Buildings[w.OfKind(BuildingKind.Well)[0]];
            double cx = well.X + well.W / 2.0, cy = well.Y + well.H / 2.0;
            (int, int) Corner(int a, int b) => ((int)Math.Floor(cx + R * (a * ux + b * vx)), (int)Math.Floor(cy + R * (a * uy + b * vy)));
            var cs = new[] { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) };
            var ring = new List<int>();
            for (int i = 0; i < 4; i++)
                foreach (int c in Line(w, cs[i].Item1, cs[i].Item2, cs[(i + 1) % 4].Item1, cs[(i + 1) % 4].Item2)) if (!ring.Contains(c)) ring.Add(c);
            if (!ring.All(w.CanFence)) continue;
            bool Within(double x, double y) => Math.Abs((x - cx) * ux + (y - cy) * uy) < R + 1 && Math.Abs((x - cx) * vx + (y - cy) * vy) < R + 1;
            bool other = false;
            foreach (var b in w.Buildings)
                if (b.Id != well.Id && new[] { (b.X, b.Y), (b.X + b.W, b.Y), (b.X, b.Y + b.H), (b.X + b.W, b.Y + b.H) }.Any(p => Within(p.Item1, p.Item2))) other = true;
            if (other) continue;
            double hx = 0, hy = 0;
            foreach (int hb in w.Houses) { hx += w.Buildings[hb].X; hy += w.Buildings[hb].Y; }
            hx /= w.Houses.Length; hy /= w.Houses.Length;
            int side = (hx - cx) * vx + (hy - cy) * vy > 0 ? 1 : -1;
            double clickX = cx + R * side * vx + 0.3 * ux, clickY = cy + R * side * vy + 0.3 * uy;
            Run(w, 4);
            w.PlaceFence(ring.ToArray());
            var target = w.GateTarget(clickX, clickY);
            bool placed = target.Cells.Length > 0 && w.PlaceGate(target.Cells[0]);
            return (w, seed, ring, target, placed, well);
        }
        return null;
    }
}
