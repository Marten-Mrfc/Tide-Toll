using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// River rules, third pass, step 2: bugs confirmed by the last bug hunt, each with its test. Gate pairs are stored per
/// cell (not found by adjacency); the river's boat stream has its own seed; growth says when its pre-sized room is used
/// up; rain, mud and growth are off and stay off while the river rules are on. (The weather seed of large seeds is
/// proven against the toy in ToyV2ParityTests.TheWeatherStreamOfLargeSeedsMatchesTheToy.)
/// </summary>
public class RuleFixes3Tests
{
    private const int N = 128;
    private static readonly SimParams Toy = SimParams.Toy;

    /// <summary>An open patch of meadow away from buildings and the river.</summary>
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

    /// <summary>
    /// Three fence lines on seed 1 and gates placed in a fixed order (found by a random search over fence shapes): a two-cell
    /// gate ends up 4-adjacent to a one-cell gate. The toy's adjacency rule took the neighbour as part of the pair: the tool
    /// named three cells, and removing the pair removed the neighbour too (or, from the other side, orphaned a partner).
    /// </summary>
    private static World GatesSideBySide()
    {
        var w = new World(1, 0, Toy);
        (int, int)[] fence =
        {
            (31,36),(32,36),(32,35),(33,35),(34,35),(34,34),(35,34),(36,34),(36,33),(37,33),(38,33),(38,32),(39,32),(37,31),(36,31),
            (36,32),(35,33),(35,35),(34,36),(34,37),(33,38),(33,37),(33,36),(32,34),(32,33),
        };
        (int, int)[] order =
        {
            (36,34),(34,35),(34,37),(32,34),(35,35),(36,33),(34,36),(33,38),(35,33),(37,33),(38,33),(33,36),(32,35),(37,31),(34,34),
            (38,32),(32,36),(35,34),(33,35),(36,32),(32,33),(36,31),(33,37),(39,32),(31,36),
        };
        w.PlaceFence(fence.Select(p => p.Item2 * N + p.Item1).ToArray());
        foreach (var (x, y) in order) w.PlaceGate(y * N + x);
        return w;
    }

    private static (int A, int B, int Single) PairBesideASingle(World w)
    {
        for (int c = 0; c < N * N; c++)
        {
            if (w.Gate[c] == 0 || w.GatePartner(c) < 0) continue;
            foreach (int nb in new[] { c + 1, c - 1, c + N, c - N })
                if (nb >= 0 && nb < N * N && w.Gate[nb] == 1 && w.GatePartner(nb) < 0) return (c, w.GatePartner(c), nb);
        }
        return (-1, -1, -1);
    }

    [Fact]
    public void RemovingATwoCellGateLeavesTheOneCellGateBesideItAlone()
    {
        var w = GatesSideBySide();
        var (a, b, single) = PairBesideASingle(w);
        Assert.True(a >= 0, "the fixed layout no longer puts a pair beside a one-cell gate");
        Assert.Equal(a, w.GatePartner(b));
        Assert.Equal(1, w.GatePair[a]);
        Assert.Equal(0, w.GatePair[single]);
        // The tool names the pair's two cells, not the neighbour.
        var t = w.GateTarget(a % N + 0.5, a / N + 0.5);
        Assert.True(t.Remove);
        Assert.Equal(new[] { a, b }.OrderBy(c => c), t.Cells.OrderBy(c => c));
        Assert.True(w.RemoveGate(a));
        Assert.True(w.Gate[a] == 0 && w.Gate[b] == 0 && w.GatePartner(a) == -1 && w.GatePartner(b) == -1 && w.Fence[a] == 1 && w.Fence[b] == 1);
        Assert.Equal(1, w.Gate[single]);
    }

    [Fact]
    public void RemovingTheOneCellGateLeavesThePairBesideItWhole()
    {
        var w = GatesSideBySide();
        var (a, b, single) = PairBesideASingle(w);
        Assert.True(a >= 0);
        var t = w.GateTarget(single % N + 0.5, single / N + 0.5);
        if (t.Remove && t.Cells.Contains(single)) Assert.Equal(new[] { single }, t.Cells);
        Assert.True(w.RemoveGate(single));
        Assert.Equal(0, w.Gate[single]);
        Assert.True(w.Gate[a] == 1 && w.Gate[b] == 1 && w.GatePair[a] == 1 && w.GatePair[b] == 1);
        Assert.Equal(b, w.GatePartner(a));
    }

    [Fact]
    public void ErasingHalfAPairFreesItsPartnerOnly()
    {
        var w = new World(1, 0, Toy);
        var (ox, oy) = OpenPatch(w);
        var stair = new List<int>();
        w.Grid.Line4(ox, oy, ox + 9, oy + 9, stair);
        w.PlaceFence(stair.ToArray());
        int at = stair.First(c => w.GateCellsFor(c)?.Length == 2);
        var pair = w.GateCellsFor(at)!;
        w.PlaceGate(at);
        Assert.Equal(1, w.Erase(new[] { pair[0] }));
        Assert.Equal(-1, w.GatePartner(pair[1]));
        Assert.Equal(1, w.Gate[pair[1]]);
        Assert.Equal(0, w.GatePair[pair[1]]);
        // Its partner is a one-cell gate now: removing it removes that cell only.
        Assert.True(w.RemoveGate(pair[1]));
        Assert.Equal(1, w.Fence[pair[1]]);
    }

    [Fact]
    public void TheBoatStreamHasItsOwnSeed()
    {
        // Until pass 3 the boat stream started where the map generator's does (seed * 7919 + 13).
        for (int seed = -50; seed <= 5000; seed++)
        {
            uint r = World.RiverStreamSeed(seed);
            Assert.NotEqual(unchecked((uint)(seed * 7919 + 13)), r);
            Assert.NotEqual(unchecked((uint)(seed * 7919 + 101)), r); // the spring-and-ice stream
            Assert.NotEqual(unchecked((uint)(seed * 31 + 5)), r);     // the walkers'
        }
    }

    [Fact]
    public void GrowthSaysWhenItsRoomIsUsedUp()
    {
        // A Hamlet target just above the town, households of 10 (so 22 slots), a cottage tried every 3 s, and the walker
        // count set back to 200 after each try: houses keep coming until the pre-sized room is full, and then growth says so.
        var p = Toy with { GrowthOn = true, HamletTarget = 215, MaxWalkers = 215, HouseholdSize = 10, HouseEverySimSec = 3, HouseFirstSimSec = 3 };
        var w = new World(2, 200, p);
        int slots = p.MaxGrownHouses;
        Assert.Equal(22, slots);
        Assert.Equal(slots, w.GrowthRoomLeft);
        Assert.False(w.GrowthRoomFull);
        for (int t = 0; t < 40 * w.Params.TicksPerDay && !w.GrowthRoomFull; t += 30)
        {
            for (int k = 0; k < 30; k++) w.Tick();
            w.SetWalkerCount(200);
        }
        Assert.True(w.GrowthRoomFull, $"{w.Grown.Length} houses grown of {slots} slots");
        Assert.Equal(slots, w.Grown.Length);
        Assert.Equal(0, w.GrowthRoomLeft);
        ToyHarness.Run(w, 3);
        Assert.Equal(slots, w.Grown.Length);
        // Without lowering the walker count by hand the room is never used up: the town stops at its target first.
        var q = new World(2, 200, p);
        ToyHarness.Run(q, 10);
        Assert.False(q.GrowthRoomFull);
        Assert.True(q.Walkers.Length + p.HouseholdSize > p.HamletTarget);
    }

    [Fact]
    public void RainMudAndGrowthAreOffAndStayOffWithTheRiverRules()
    {
        // SimParams.Default has rain and growth on; the river rules turn both off, through a whole year with floods.
        var p = SimParams.Default with { River = RiverRules.Default with { StartMoney = 500, FloodInFirstSpring = true, FloodChance = 1 } };
        Assert.True(p.RainOn && p.GrowthOn);
        var w = new World(3, 200, p);
        Assert.False(w.RainOn);
        Assert.False(w.GrowthRoomFull);
        Assert.Equal(0, w.GrowthRoomLeft);
        int walkers = w.Walkers.Length, houses = w.Houses.Length, buildings = w.Buildings.Length;
        w.StartRain(); // does nothing with rain off
        for (int day = 0; day < 4 * 15 + 2; day++)
        {
            ToyHarness.Run(w, 1);
            Assert.False(w.Weather.Raining, $"rain on day {day}");
            Assert.Equal(0.0, w.Weather.Wetness);
            Assert.Equal(0, w.MudCells);
            Assert.Equal(0, w.Weather.RainDays);
            Assert.Equal(0, w.Grown.Length);
            Assert.Equal(walkers, w.Walkers.Length);
            Assert.Equal(houses, w.Houses.Length);
            Assert.Equal(buildings, w.Buildings.Length);
        }
        foreach (byte m in w.Mud) Assert.Equal(0, m);
        Assert.True(w.River!.Stats.Crossings > 0);
    }
}
