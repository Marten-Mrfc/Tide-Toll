using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Green pass: touches, the game builders' wishes, the tracks that go on (docs/plans/town-sim-api.md "Added in the green pass").</summary>
public class TownGreenTests
{
    private static void MarkLine(bool[] m, int n, double[] px, double[] py)
    {
        for (int i = 0; i + 1 < px.Length; i++)
        {
            double dx = px[i + 1] - px[i], dy = py[i + 1] - py[i];
            int steps = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) * 8) + 1;
            for (int s = 0; s <= steps; s++)
            {
                int x = (int)Math.Floor(px[i] + dx * s / steps), y = (int)Math.Floor(py[i] + dy * s / steps);
                if (x >= 0 && y >= 0 && x < n && y < n) m[y * n + x] = true;
            }
        }
    }

    [Fact]
    public void TouchSpotsAreClearOfRoadsPlotsWaterAndEachOtherOnSeeds1To100()
    {
        var bad = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(1, 101, new ParallelOptions { MaxDegreeOfParallelism = 3 }, seed =>
        {
            var tp = TownPlanner.Build(seed);
            var rp = TownRiverPlanner.Build(tp);
            var spots = TownTouchPlanner.Build(tp);
            int n = tp.Map.Size;
            var used = new bool[n * n];
            foreach (var p in tp.Plots) for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) used[y * n + x] = true;
            foreach (var p in rp.Plots) for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) if (x >= 0 && y >= 0 && x < n && y < n) used[y * n + x] = true;
            foreach (var r in tp.Roads) MarkLine(used, n, r.Px, r.Py);
            foreach (var l in rp.Links) MarkLine(used, n, l.Px, l.Py);
            if (spots.Length < 30) bad.Add($"seed {seed}: only {spots.Length} spots");
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                int c = s.CellY * n + s.CellX;
                if (tp.Map.Water[c] != 0) bad.Add($"seed {seed} spot {i}: on water");
                if (used[c]) bad.Add($"seed {seed} spot {i}: on a road, lane or plot");
                if ((s.Plot < 0) == (s.RiverPlot < 0)) bad.Add($"seed {seed} spot {i}: belongs to no plot or two");
                if (!Enum.IsDefined(s.Kind)) bad.Add($"seed {seed} spot {i}: kind {s.Kind}");
                if (Math.Abs(s.X - (s.CellX + 0.5)) > 1e-9 || double.IsNaN(s.Heading)) bad.Add($"seed {seed} spot {i}: position or heading");
                for (int j = 0; j < i; j++)
                    if (Math.Abs(spots[j].CellX - s.CellX) <= 1 && Math.Abs(spots[j].CellY - s.CellY) <= 1) bad.Add($"seed {seed} spots {j} and {i} touch");
            }
            // Deterministic.
            var again = TownTouchPlanner.Build(TownPlanner.Build(seed));
            if (again.Length != spots.Length) bad.Add($"seed {seed}: not deterministic");
            else for (int i = 0; i < spots.Length; i++) if (again[i].CellX != spots[i].CellX || again[i].CellY != spots[i].CellY || again[i].Kind != spots[i].Kind) bad.Add($"seed {seed}: not deterministic");
        });
        Assert.True(bad.IsEmpty, string.Join("\n", bad.Take(30)));
    }

    [Fact]
    public void TheKindNamesMatchTheArt()
    {
        Assert.Equal(new[] { "Woodpile", "WashingLine", "FlowerBed", "HenCoop", "Bench", "Beehives", "Haystack", "AppleTree", "Handcart", "Signpost" }, Enum.GetNames<TouchKind>());
    }

    [Fact]
    public void TouchesAppearOnTheCoinBeatWithAnEventAndSurviveASave()
    {
        var t = new T(1);
        var bot = new TownBot(BotKind.Payback, 1);
        var ev = new List<TownEvent>();
        int seen = 0;
        for (int k = 0; k < 12 * 60 * TownRules.TickHz; k++)
        {
            t.Step(); bot.Act(t); t.DrainEvents(ev);
            foreach (var e in ev)
                if (e.Kind == TownEventKind.TouchAdded)
                {
                    var v = t.Touch(e.Index);
                    Assert.Equal(seen, e.Index);
                    Assert.Equal((int)v.Kind, e.Level);
                    Assert.Equal(v.X, e.X); Assert.Equal(v.Y, e.Y); Assert.Equal(v.Plot, e.Plot);
                    Assert.True(v.Plot >= 0 && t.Plan.Plots[v.Plot].Level <= t.Level);
                    Assert.Equal(v.Kind.ToString(), e.Text);
                    seen++;
                }
        }
        Assert.Equal(seen, t.TouchCount);
        Assert.InRange(seen, 2, 12);
        // A save carries the touches and the beat exactly.
        var b = TownSave.Load(TownSave.Save(t), t.Plan);
        Assert.Equal(t.TouchCount, b.TouchCount);
        Assert.Equal(t.NextTouchAtCoin, b.NextTouchAtCoin);
        for (int k = 0; k < 3 * 60 * TownRules.TickHz; k++) { t.Step(); b.Step(); }
        Assert.Equal(TownSave.Save(t), TownSave.Save(b));
        // A save from before the green pass (no touch fields) still loads, and touches start from there.
        var json = TownSave.Save(t);
        int at = json.IndexOf("\"TouchCount\"", StringComparison.Ordinal);
        int end = json.IndexOf("\"TouchPrevRate\"", StringComparison.Ordinal);
        Assert.True(at > 0 && end > at);
        var old = TownSave.Load(json.Remove(at, json.IndexOf(',', end) + 1 - at), t.Plan);
        Assert.Equal(0, old.TouchCount);
        for (int k = 0; k < 600; k++) old.Step();
    }

    [Fact]
    public void ShopItemsSayLockedAndFinished()
    {
        var t = new T(1);
        var items = new List<ShopItem>(); t.ShopItems(items);
        var load = items.First(x => x.Kind == ShopItemKind.Load);
        Assert.True(load.Locked); Assert.False(load.Finished);
        var vil = items.First(x => x.Kind == ShopItemKind.Villager);
        Assert.False(vil.Locked); Assert.False(vil.Finished);
        Assert.True(T.IsFinishedReason("Fastest pace reached"));
        Assert.True(T.IsLockedReason("Unlocks at level 7"));
        Assert.False(T.IsLockedReason("Not enough coin"));
        Assert.False(T.IsFinishedReason("Not enough coin"));
    }

    [Fact]
    public void NowCallsIncludeTheCoinAndOffersSayIfBought()
    {
        var t = new T(1);
        for (int r = 0; r < t.Plan.Roads.Length; r++) Assert.False(t.CanPaveNow(r));
        foreach (var p in t.Plan.Plots) Assert.False(t.CanExtendNow(p.Id));
        Assert.False(t.OfferBoughtOf(OfferKind.MarketStall));
        Assert.False(t.OfferBoughtOf(OfferKind.Mill));
    }

    [Fact]
    public void AchievementTextsUseThousandsSeparators()
    {
        foreach (var a in TownAchievements.All)
            Assert.DoesNotMatch(@"\d{4}", a.Description);
        var t = new T(1);
        for (int i = 0; i < 16; i++) Assert.DoesNotMatch(@"\d{4}", t.RiverAchievement(i).Description);
    }

    [Fact]
    public void TheHintBuildsNoStringPerRead()
    {
        var t = new T(3);
        var bot = new TownBot(BotKind.Cheapest, 3);
        for (int k = 0; k < 8 * 60 * TownRules.TickHz; k++)
        {
            t.Step(); bot.Act(t);
            if (k % 50 != 0) continue;
            string a = t.Hint;
            long b0 = GC.GetAllocatedBytesForCurrentThread();
            string b = t.Hint;
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - b0);
            Assert.Same(a, b);
            // Never points at something that cannot be bought (ignoring coin).
            if (t.HintPlot >= 0 && b.StartsWith("Every cottage is full", StringComparison.Ordinal)) Assert.True(t.CanExtend(t.HintPlot, out _));
            if (t.HintRoad >= 0) Assert.True(t.CanPave(t.HintRoad, out _));
        }
    }

    [Fact]
    public void TheOldTracksGoOnInTheSecondAgeAndTheSealSaysTheAgeIsComplete()
    {
        var t = new T(2);
        var bot = new TownBot(BotKind.Payback, 2) { PlayOn = true };
        var ev = new List<TownEvent>();
        bool beyond = false;
        while (!t.HasSeal && t.Time < TownRiverTargets.PlayMinutesMax * 60)
        {
            t.Step(); bot.Act(t);
            if (t.Tick % 50 == 0) t.DrainEvents(ev);
            if (t.InRiverAge && (t.PaceLevel > TownRules.PaceMax || t.ToolsLevel > TownRules.ToolsMax || t.ValueLevel(Good.Water) > TownRules.ValueMax)) beyond = true;
        }
        Assert.True(t.HasSeal);
        Assert.True(beyond, "no step of Pace, Tools or Water price beyond the first age's last was bought in the second age");
        Assert.True(t.SecondAgeComplete);
        Assert.NotEqual("", t.SecondAgeCompleteText);
        var left = new List<ShopItem>(); t.StillOnSale(left);
        Assert.NotEmpty(left);
        Assert.All(left, x => Assert.False(x.Finished));
        var b = TownSave.Load(TownSave.Save(t), t.Plan);
        Assert.True(b.SecondAgeComplete);
        Assert.Equal(t.StillOnSaleCount, b.StillOnSaleCount);
        Assert.InRange(t.TouchCount, 25, TownRules.TouchMax);
    }
}
