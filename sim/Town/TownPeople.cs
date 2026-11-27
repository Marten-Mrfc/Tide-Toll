using System;

namespace TideToll.Sim.Town;

/// <summary>The kind of work a villager leans toward (n7 step 1). Appended only: saves and trades (step 2) will read these numbers.</summary>
public enum Leaning : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5 }

/// <summary>
/// Villagers as people (n7 step 1): a name and a small natural leaning for each villager, both pure functions of the
/// town's seed and the villager's number (never stored, never drawn from a random stream at play time), and the two
/// counters no rule reads (trips made, coin earned; in <see cref="VillagerState"/>).
/// </summary>
public sealed partial class Town
{
    /// <summary>Plain old-world given names written for this game (no real people, no trademarks).</summary>
    internal static readonly string[] GivenNames =
    {
        "Ada", "Agnes", "Alda", "Aldith", "Alric", "Amice", "Ansel", "Avice", "Bennet", "Bertil",
        "Bram", "Bryn", "Cecily", "Clem", "Colin", "Cora", "Dunn", "Edda", "Edith", "Edwin",
        "Elfa", "Elric", "Emma", "Ewan", "Fenn", "Gerda", "Giles", "Godwin", "Greta", "Gunnild",
        "Hal", "Hamo", "Hawise", "Hilda", "Hob", "Hugh", "Ida", "Ivo", "Joan", "Jory",
        "Joss", "Kit", "Lettice", "Lora", "Mabel", "Madoc", "Maud", "Merry", "Milo", "Ned",
        "Nell", "Odo", "Olwen", "Osric", "Perrin", "Piers", "Quenild", "Ralf", "Rohese", "Rowan",
        "Sabine", "Simkin", "Sybil", "Tamsin", "Tib", "Tobin", "Udo", "Ulric", "Wat", "Wenna",
        "Wilmot", "Wynn", "Yolande", "Aveline", "Brice", "Dorcas", "Hew", "Isolda", "Letty", "Oswy",
    };

    /// <summary>By-names for a second, third ... villager of the same given name, in that order.</summary>
    internal static readonly string[] ByNames =
    {
        "the younger", "of the hill", "the tall", "the quiet", "the red", "of the ford",
        "the small", "the elder", "of the green", "the bold", "of the brook", "the fair",
    };

    private string[] _names = Array.Empty<string>();

    /// <summary>Tests: switch the blind-spot fixes on or off for this town alone (null: TownRules decides), so a test
    /// never changes the rules other tests run under.</summary>
    internal bool? WaitFixesHere;
    private bool MillFixNow => WaitFixesHere ?? (S.River == null ? TownRules.MillWaitCountsOnTheWay : TownRules.MillWaitCountsOnTheWayInSecondAge);
    private bool SiteFixNow => WaitFixesHere ?? TownRules.SiteWaitCountsOnTheWay;

    /// <summary>A pure 32-bit mix of the seed, the villager's number and a salt (no state, no random stream).</summary>
    private static uint PeopleHash(int seed, int id, uint salt)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)id * 0x85EBCA77u ^ salt * 0xC2B2AE3Du;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return h;
        }
    }

    /// <summary>The leaning of villager <paramref name="id"/> in a town of <paramref name="seed"/>: one of the five kinds of work.</summary>
    public static Leaning LeaningOf(int seed, int id) => (Leaning)(1 + PeopleHash(seed, id, 0x1EA4u) % 5);

    /// <summary>Which leaning a job belongs to (None for no job).</summary>
    public static Leaning LeaningOfJob(JobKind j) => j switch
    {
        JobKind.Water => Leaning.Water,
        JobKind.GrainHome or JobKind.GrainMarket or JobKind.GrainBoat => Leaning.Grain,
        JobKind.Flour or JobKind.FlourBoat => Leaning.Flour,
        JobKind.WoolBoat or JobKind.WoolMarket => Leaning.Wool,
        JobKind.ClothBoat or JobKind.ClothMarket => Leaning.Cloth,
        _ => Leaning.None,
    };

    /// <summary>Index into <see cref="GivenNames"/> of villager <paramref name="id"/>'s given name.</summary>
    private static int GivenIndex(int seed, int id) => (int)(PeopleHash(seed, id, 0x4A3Eu) % (uint)GivenNames.Length);

    /// <summary>The name of villager <paramref name="id"/>: a given name, with a by-name when an earlier villager has it too.
    /// A pure function of the seed and the number; built once per villager (not in the tick).</summary>
    public static string VillagerNameOf(int seed, int id)
    {
        int g = GivenIndex(seed, id), before = 0;
        for (int j = 0; j < id; j++) if (GivenIndex(seed, j) == g) before++;
        if (before == 0) return GivenNames[g];
        return before <= ByNames.Length ? GivenNames[g] + " " + ByNames[before - 1] : GivenNames[g] + " " + ByNames[(before - 1) % ByNames.Length] + " " + (before / ByNames.Length + 1);
    }

    /// <summary>The name of villager <paramref name="i"/> (kept once made, so asking again allocates nothing).</summary>
    public string VillagerName(int i)
    {
        if (i < 0 || i >= S.Villagers.Length) return "";
        if (_names.Length <= i)
        {
            var n = new string[Math.Max(S.Villagers.Length, 8)];
            Array.Copy(_names, n, _names.Length);
            _names = n;
        }
        return _names[i] ??= VillagerNameOf(S.Seed, i);
    }

    /// <summary>Whether a leaning changes anything in play now (the bonus is above 0); the card says a leaning only then.</summary>
    public bool LeaningCounts => TownRules.LeanBonus > 0;

    /// <summary>Villager <paramref name="i"/>'s leaning.</summary>
    public Leaning VillagerLeaning(int i) => LeaningOf(S.Seed, i);

    /// <summary>The leaning of the villager choosing a job now (set by ChooseJob; read by Consider and RConsider).</summary>
    private Leaning _choosing;

    /// <summary>A job's rate as the choosing villager weighs it: its own kind of work counts a little more.</summary>
    internal double LeanedFor(int id, JobKind j, double rate)
    {
        var keep = _choosing; var keepT = _choosingTrade;
        _choosing = LeaningOf(S.Seed, id); _choosingTrade = S.Villagers.Length > id ? S.Villagers[id].Trade : Trade.None;
        double r = Leaned(j, rate);
        _choosing = keep; _choosingTrade = keepT;
        return r;
    }

    /// <summary>The trade of the villager choosing a job now (set by ChooseJob; None for a Helper).</summary>
    private Trade _choosingTrade;

    private double Leaned(JobKind j, double rate)
    {
        if (_choosing != Leaning.None && LeaningOfJob(j) == _choosing) rate *= 1 + TownRules.LeanBonus;
        if (_choosingTrade != Trade.None && TradeOfJob(j) == _choosingTrade) rate *= TownRules.TradeLean; // n7 trades: its own trade
        return rate;
    }
}
