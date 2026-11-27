using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Villagers as people (n7 step 1, docs/plans/n7/people-build-sheet.md part 1): the two blind spots, the leaning,
/// the name, the two counters, the views and the save.</summary>
public class TownPeopleTests
{
    private static T Play(int seed, double minutes, BotKind kind = BotKind.Payback, bool playOn = false)
    {
        var t = new T(seed);
        var bot = new TownBot(kind, seed) { PlayOn = playOn };
        long n = (long)(minutes * 60 * TownRules.TickHz);
        for (long k = 0; k < n; k++) { t.Step(); bot.Act(t); }
        return t;
    }

    [Fact]
    public void NamesAndLeaningsArePureFunctionsOfSeedAndNumber()
    {
        for (int seed = 1; seed <= 3; seed++)
            for (int id = 0; id < 200; id++)
            {
                Assert.Equal(T.VillagerNameOf(seed, id), T.VillagerNameOf(seed, id));
                Assert.Equal(T.LeaningOf(seed, id), T.LeaningOf(seed, id));
                Assert.InRange((int)T.LeaningOf(seed, id), 1, 5);
            }
        // Pinned: a change of the table or the hash renames every villager of every saved town.
        Assert.Equal(new[] { "Amice", "Wenna", "Ada", "Joss" }, Enumerable.Range(0, 4).Select(i => T.VillagerNameOf(1, i)).ToArray());
        // Another seed, other people.
        Assert.NotEqual(Enumerable.Range(0, 10).Select(i => T.VillagerNameOf(1, i)), Enumerable.Range(0, 10).Select(i => T.VillagerNameOf(2, i)));
        // Every kind of work has its people.
        for (int k = 1; k <= 5; k++) Assert.True(Enumerable.Range(0, 200).Count(i => (int)T.LeaningOf(1, i) == k) >= 25);
    }

    [Fact]
    public void ASharedGivenNameGetsAByName()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var names = Enumerable.Range(0, 200).Select(i => T.VillagerNameOf(seed, i)).ToArray();
            Assert.Equal(names.Length, names.Distinct().Count());
            Assert.Contains(names, n => n.EndsWith(" the younger", StringComparison.Ordinal));
            Assert.All(names, n => Assert.Contains(n.Split(' ')[0], T.GivenNames));
        }
    }

    [Fact]
    public void TheViewGivesNameLeaningCountersAndWork()
    {
        var t = Play(2, 8);
        Assert.True(t.VillagerCount >= 4);
        long trips = 0;
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            var s = t.S.Villagers[i];
            Assert.Equal(T.LeaningOf(2, i), v.Leaning);
            Assert.Equal(T.VillagerNameOf(2, i), t.VillagerName(i));
            Assert.Same(t.VillagerName(i), t.VillagerName(i)); // kept: asking again allocates nothing
            Assert.Equal(s.Slot, v.Slot); Assert.Equal(s.Stage, v.Stage); Assert.Equal(s.WorkLeft, v.WorkLeft); Assert.Equal(s.Dir, v.Dir);
            Assert.Equal(s.Trips, v.Trips); Assert.Equal(s.Earned, v.Earned);
            if (v.Phase == VillagerPhase.Working) Assert.True(v.WorkLeft > 0 && v.Slot >= 0);
            trips += v.Trips;
        }
        Assert.Equal(t.Trips, trips);
        Assert.True(t.S.Villagers[0].Earned > 0);
        Assert.Equal("", t.VillagerName(-1));
        Assert.Equal("", t.VillagerName(t.VillagerCount));
    }

    [Fact]
    public void CountersAddUpOverBothAges()
    {
        var t = Play(3, 50, playOn: true);
        Assert.True(t.InRiverAge);
        long trips = 0; double earned = 0;
        foreach (var v in t.S.Villagers) { trips += v.Trips; earned += v.Earned; }
        Assert.Equal(t.Trips, trips);
        Assert.True(earned > 0 && earned <= t.CoinEarned + 1e-6, $"earned {earned} of {t.CoinEarned}");
    }

    [Fact]
    public void ALeaningCountsItsOwnKindOfWorkALittleMore()
    {
        var t = new T(1);
        for (int id = 0; id < 20; id++)
            foreach (var j in new[] { JobKind.Water, JobKind.GrainHome, JobKind.GrainMarket, JobKind.Flour, JobKind.GrainBoat, JobKind.FlourBoat, JobKind.WoolBoat, JobKind.WoolMarket, JobKind.ClothBoat, JobKind.ClothMarket })
            {
                double want = T.LeaningOfJob(j) == T.LeaningOf(1, id) ? 10 * (1 + TownRules.LeanBonus) : 10;
                Assert.Equal(want, t.LeanedFor(id, j, 10), 12);
            }
        Assert.InRange(TownRules.LeanBonus, 0, 0.25); // "a small natural leaning" (0: off until trades give it meaning)
        Assert.Equal(TownRules.LeanBonus > 0, t.LeaningCounts);
    }

    /// <summary>Blind spot 1: flour pickers on their way to the field (or at it) count in the mill's wait.</summary>
    [Fact]
    public void TheMillsWaitCountsFlourPickersStillAtTheField()
    {
        var t = new T(1);
        var bot = new TownBot(BotKind.Payback, 1);
        int seen = 0;
        for (long k = 0; k < 40L * 60 * TownRules.TickHz && seen < 50; k++)
        {
            t.Step(); bot.Act(t);
            if (k % 50 != 0) continue;
            int mill = -1;
            for (int i = 0; i < t.WorkplaceCount; i++) if (t.Workplace(i).Open && t.Workplace(i).Kind == PlotKind.Mill) mill = i;
            if (mill < 0) continue;
            var w = t.S.Workplaces[mill];
            int early = t.S.Villagers.Count(o => o.Stage == 0 && o.Job == JobKind.Flour);
            if (early == 0) continue;
            seen++;
            var probe = new VillagerState { Id = -1, Workplace = -1 };
            int walking = t.S.Villagers.Count(o => o.Workplace == mill && o.Phase == VillagerPhase.Walking);
            int over = w.Busy + w.QueueLen + walking + early - w.Places + 1;
            double want = over <= 0 ? 0 : over * t.WorkTime(PlotKind.Mill) / t.ToolsFactor / w.Places;
            Assert.Equal(want, t.ExpectedWait(mill, probe), 9);
        }
        Assert.True(seen >= 10, $"only {seen} moments with flour pickers at the field");
    }

    /// <summary>Blind spot 2: those on their way to the pasture or the weaver count in its wait.</summary>
    [Fact]
    public void ThePastureAndWeaverWaitsCountThoseOnTheWay()
    {
        var t = new T(1);
        var bot = new TownBot(BotKind.Payback, 1) { PlayOn = true };
        int seen = 0;
        for (long k = 0; k < 90L * 60 * TownRules.TickHz && seen < 40 && !t.HasSeal; k++)
        {
            t.Step(); bot.Act(t);
            if (k % 50 != 0 || !t.InRiverAge) continue;
            for (int site = 0; site < 2; site++)
            {
                var probe = new VillagerState { Id = -1 };
                int c = site == 0 ? t.S.River!.PastureBusy : t.S.River!.WeaverBusy, onWay = 0;
                foreach (var o in t.S.Villagers)
                {
                    if (o.RMode != 0 && o.Phase == VillagerPhase.Queued && o.RSite == site) { c++; continue; }
                    bool wool = o.Job is JobKind.WoolBoat or JobKind.WoolMarket, cloth = o.Job is JobKind.ClothBoat or JobKind.ClothMarket;
                    // Not yet sheared (pasture) or not yet woven (weaver), and not standing at that site.
                    bool ahead = (site == 0 ? wool || cloth : cloth) && o.RSite != site && !PastSite(o, site);
                    if (ahead && !TownRules.SiteWaitCountsOnTheWay) { onWay++; continue; } // switched off: not counted
                    if (ahead) Assert.True(site == 0 ? o.Carry == Good.None : o.Carry != Good.Cloth);
                    if (ahead) { c++; onWay++; }
                }
                if (onWay == 0) continue;
                seen++;
                int places = site == 0 ? TownRulesRiver.PasturePlaces : TownRulesRiver.WeaverPlaces;
                int over = c - places + 1;
                double want = over <= 0 ? 0 : over * (site == 0 ? TownRulesRiver.ShearTime : TownRulesRiver.WeaveTime) / t.ToolsFactor / places;
                Assert.Equal(want, t.SiteWait(site, probe), 9);
            }
        }
        Assert.True(seen >= 5, $"only {seen} moments with villagers on their way to a site");
    }

    /// <summary>True when the villager's itinerary has no work step at the site left (done there already).</summary>
    private static bool PastSite(VillagerState o, int site)
    {
        for (int k = o.RPos; k < o.RLen; k++) if (o.RSteps[k] == 3000 + site) return false;
        return true;
    }

    /// <summary>The measured point of step 1: the whole town on one job is rarer in the second age (it was 63 % of the
    /// time on seeds 1 to 3 with the payback bot before the blind spots were fixed; the research's figure).</summary>
    [Fact]
    public void FewerSpellsOfTheWholeTownOnOneJob()
    {
        if (!TownRules.SiteWaitCountsOnTheWay) return; // the second age's fix switched off (TownRules): nothing to hold
        int samples = 0, herd = 0;
        var byJob = new int[16];
        for (int seed = 1; seed <= 3; seed++)
        {
            var t = new T(seed);
            var bot = new TownBot(BotKind.Payback, seed) { PlayOn = true };
            for (long k = 0; k < 120L * 60 * TownRules.TickHz && !t.HasSeal; k++)
            {
                t.Step(); bot.Act(t);
                if (!t.InRiverAge || t.Tick % (10 * TownRules.TickHz) != 0 || t.VillagerCount < 4) continue;
                Array.Clear(byJob);
                for (int i = 0; i < t.VillagerCount; i++) byJob[(int)t.Villager(i).Job]++;
                samples++;
                if (byJob.Max() * 4 > t.VillagerCount * 3) herd++;
            }
        }
        Assert.True(herd < samples * 0.5, $"{herd} of {samples} samples with more than three quarters on one job");
    }

    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Town", "data", name));

    /// <summary>The save: saves written before the counters existed (versions 2 and 3, kept as test data) load with the
    /// counters at nought, names and leanings derived; the town plays on, and saves and loads exactly from then on.</summary>
    [Theory]
    [InlineData("town-v2-seed3-12min.json", 2)]
    [InlineData("town-v3-seed3-river6min.json", 3)]
    public void OldSavesLoadWithCountersAtNought(string file, int version)
    {
        string json = Data(file);
        Assert.DoesNotContain("\"Trips\":0,\"Earned\"", json); // written before the counters
        var t = TownSave.Load(json);
        Assert.Equal(version, t.S.Version);
        Assert.Equal(version == 3, t.InRiverAge);
        Assert.True(t.VillagerCount >= 4);
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            Assert.Equal(0, v.Trips); Assert.Equal(0, v.Earned);
            Assert.Equal(T.LeaningOf(3, i), v.Leaning);
            Assert.Equal(T.VillagerNameOf(3, i), t.VillagerName(i));
        }
        var bot = new TownBot(BotKind.Payback, 3) { PlayOn = true };
        for (int k = 0; k < 3 * 60 * TownRules.TickHz; k++) { t.Step(); bot.Act(t); }
        Assert.True(t.S.Villagers.Sum(v => v.Trips) > 0);
        string again = TownSave.Save(t);
        Assert.Contains("\"Trips\":", again);
        var u = TownSave.Load(again);
        Assert.Equal(again, TownSave.Save(u));
        for (int k = 0; k < 60 * TownRules.TickHz; k++) { t.Step(); u.Step(); }
        Assert.Equal(TownSave.Save(t), TownSave.Save(u));
    }

    /// <summary>What ships in step 1: the second age plays exactly as before. A saved second-age town (written by the code
    /// before n7) played on 10 minutes by the payback bot gives the same save as with every n7 switch off (the hash below
    /// was printed with them off: `people --replay ... --minutes 10 --millfix 0`).</summary>
    [Fact]
    public void TheSecondAgePlaysAsBeforeFromASavedTown()
    {
        Assert.False(TownRules.MillWaitCountsOnTheWayInSecondAge);
        Assert.False(TownRules.SiteWaitCountsOnTheWay);
        Assert.Equal(0, TownRules.LeanBonus);
        var t = TownSave.Load(Data("town-v3-seed3-river6min.json"));
        var bot = new TownBot(BotKind.Payback, 3) { PlayOn = true };
        for (long k = 0; k < 10L * 60 * TownRules.TickHz; k++) { t.Step(); bot.Act(t); }
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(TownSave.Save(t)));
        Assert.Equal("C2A8E8351FA55DF94E3D8EB4B13F8082CA217FD37149BE761FC76F452AAB8285", Convert.ToHexString(hash));
    }
}
