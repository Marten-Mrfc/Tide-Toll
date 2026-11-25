namespace TideToll.Sim.Town;

/// <summary>
/// Every number of the first age in one place. The balancer tunes these and nothing else.
/// Times are game seconds, distances are map cells, money is coin.
/// The tunables are static fields (not const) so the 'town' tool can try values with --set Name=value; nothing in the
/// simulation writes them. Change them only before building a town, never while one runs.
/// </summary>
public static class TownRules
{
    // ---- Clock ----
    public const int TickHz = 10;
    public const double Dt = 1.0 / TickHz;

    // ---- Walking ----
    public static double BaseSpeed = 1.6;            // cells per second on a worn-in track (seed 1 tuned: docs/plans/town-pacing.md)
    public static double TrailSpeedFactor = 0.7;     // speed factor of a brand-new faint trail (wear 0)
    public static double GravelSpeedFactor = 1.25;
    public static double CobbleSpeedFactor = 1.5;
    public static double PassesToWearIn = 6;         // full-length walks that wear a new road in
    public static double ComfortPerCellTrack = 0.1;  // v2: was 0.22, so a town of 8 to 10 clogs its busiest unpaved road // comfortable walkers per cell of road, by surface
    public static double ComfortPerCellGravel = 0.35;
    public static double ComfortPerCellCobbles = 0.5;
    public static double MinComfort = 1.5;           // v2: was 2
    public static double CrowdSlowdown = 0.6;        // speed = 1 / (1 + k * excess / comfort)
    public static double CrowdMinFactor = 0.35;      // never slower than this share of free speed
    public static double QueueSpacing = 0.8;         // cells between people waiting at a workplace

    // ---- Goods and jobs ----
    public static double WaterValue = 2;
    public static double GrainHomeValue = 3;
    public static double GrainMarketValue = 4;
    public static double FlourValue = 10;
    public static double WellWork = 1.5;             // seconds at the well
    public static double FieldWork = 3.0;
    public static double MillWork = 3.0;
    public static int WellPlaces = 1;
    public static int SecondBucketPlaces = 1;
    public static int FieldPlaces = 2;
    public static int MillPlaces = 2;

    // ---- Housing ----
    public static int PerCottage = 2;
    public static int BaseLoad = 2;                 // units carried per trip before any Load upgrade

    // ---- Shop: price = base * growth ^ level ----
    public static double VillagerBase = 5, VillagerGrowth = 1.3;
    public static double PaceBase = 2, PaceGrowth = 1.36;
    public static double PaceStep = 0.04;          // rules v2.1: each Pace level adds 4 % walking speed (v2: 5 %) (factor = 1 + step * level)
    public static int PaceMax = 25;                // rules v2.1: Pace is finished after 25 levels (v2: 20)
    public static readonly double[] LoadPrices = { 300, 1600, 4000 };
    public static readonly string[] LoadNames = { "Basket", "Yoke", "Handcart" };
    public static readonly int[] LoadLevels = { 3, 7, 11 };   // town level that unlocks each Load step
    public static double GravelPerCell = 2.0;
    public static double CobblesPerCell = 10.0;
    public static double PaveGrowth = 1.08;          // each road paved makes the next 4 % dearer per cell
    public static int CobblesFromLevel = 9;          // cobbles are offered from this level on
    // Building offers (price), by offer id in TownPlan.Offers order.
    public static double SecondBucketPrice = 70;
    public static double MarketStallPrice = 150;
    public static double SecondFieldPrice = 1200;
    public static double MillPrice = 3000;

    // ---- Rules v2 tracks: price = base * growth ^ level ----
    public static double ToolsBase = 30, ToolsGrowth = 1.33;
    public static int ToolsMax = 18, ToolsFromLevel = 3;
    public static double ToolsStep = 0.03;          // work time = base / (1 + step * level)
    // Value tracks, by Good (index 0 unused): Water, Grain, Flour pay ValueStep more per unit per level.
    public static readonly double[] ValueBase = { 0, 12, 30, 250 };
    public static readonly double[] ValueGrowth = { 0, 1.55, 1.43, 1.17 };
    public static int ValueMax = 14;
    public static double ValueStep = 0.04;
    // Extending a cottage: each cottage twice, each extension houses one more villager.
    public static int ExtendMax = 2, ExtendFromLevel = 5;
    public static double ExtendBase = 60, ExtendGrowth = 1.33;
    // The bell track: each level adds BellTrackHurry seconds of hurry and takes BellTrackRest seconds off the rest.
    public static double BellTrackBase = 60, BellTrackGrowth = 2.6;
    public static int BellTrackMax = 5;
    public static double BellTrackHurry = 0.6, BellTrackRest = 2;

    // ---- Rules v2.1: steps on sale by town level (index 0 = level 1; the number of steps of the track that can have been
    // bought by then). Few steps early, more at later levels, the last ones near the end of the age, so no track is bought
    // out early and every level-up brings new steps. A step not yet on sale is listed with "Unlocks at level N". ----
    public static readonly int[] PaceAt = { 4, 6, 8, 10, 12, 13, 14, 15, 16, 17, 18, 20, 21, 23, 25, 25 };
    public static readonly int[] ToolsAt = { 0, 0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15, 18, 18 };
    public static readonly int[] ValueAt = { 2, 3, 4, 5, 6, 7, 7, 8, 8, 9, 10, 10, 11, 12, 14, 14 };
    public static readonly int[] FlourValueAt = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 9, 14, 14 };
    public static readonly int[] BellTrackAt = { 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5 };
    public static readonly int[] ExtendAt = { 0, 0, 0, 0, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 14, 14 };   // extensions in all (7 cottages: 14 at most)

    // ---- Requests (rules v2) ----
    public static int RequestFromLevel = 3;
    public static double RequestFirstDelay = 20;    // seconds after the unlocking level
    public static double RequestGap = 70;           // seconds from the end of one request to the next
    public static double RequestGapAfterMiss = 45;  // sooner after a miss
    public static readonly double[] RequestSeconds = { 60, 90, 120 };
    public static double RequestShare = 1.15;       // amount = this share of the coin the town earns in the time, in units of the good
    public static double RequestBonus = 1.5;        // the asked good pays this much more while the request runs
    public static double RequestReward = 0.6;       // reward coin = amount * unit value * this; renown = the reward coin
    public static int RequestMinAmount = 6;
    public static double RequestCapacityShare = 0.95; // and at most this share of what its workplaces could turn out
    // Rules v2.1: requests adapt to the player. Each request met multiplies the next amount by RequestGrowMet, each missed
    // by RequestShrinkMissed, within RequestScaleMin..Max, so any player ends up meeting about ln(1/shrink) / ln(grow/shrink)
    // of them (0.65 with 1.10 and 0.84). Saved with the town (TownState.ReqScale).
    public static double RequestGrowMet = 1.10, RequestShrinkMissed = 0.84;
    public static double RequestScaleMin = 0.3, RequestScaleMax = 2.5;

    // ---- Renown and levels ----
    public static double RenownPerCoin = 1.0;        // renown of a purchase = price * this (at least 1)
    /// <summary>Renown needed to reach level i+1 (index 0 is level 1, always 0). Sixteen levels (rules v2); the last grants the charter.</summary>
    public static readonly double[] LevelRenown = { 0, 20, 100, 300, 700, 1300, 2200, 3500, 5500, 9000, 13500, 20000, 29000, 42000, 59000, 84000 };

    // ---- The bell ----
    public static double BellHurry = 1.17;           // speed factor while ringing
    public static double BellDuration = 6;
    public static double BellRest = 40;

    // ---- Perks: every PerkEvery levels of a track ----
    public const int PerkEvery = 5;
    public static double VillagerPerkCoin = 0.05;    // "Good neighbours": +5 % coin per perk
    public static double PacePerkBell = 0.1;         // "Light feet": bell rests 10 % shorter per perk (floor 50 %)
    public static double PavePerkComfort = 0.2;      // "Mason's eye": paved roads carry 20 % more walkers per perk

    // ---- Coin per minute ----
    public const int RateWindowSeconds = 60;
}

/// <summary>The design brief's pacing targets (docs/plans/incremental-design.md), read by the pacing tests and the 'town' tool.</summary>
public static class TownTargets
{
    public const double FirstCoinBy = 25;            // seconds
    public const double FirstPurchaseBy = 45;        // seconds
    public const double EarlyPurchasesPerMinute = 4; // in each of the first EarlyMinutes minutes
    public const int EarlyMinutes = 10;
    public const double LatePurchasesPerMinute = 2;  // in each minute after that, until the charter
    public const double MaxNothingAffordable = 60;   // seconds
    public const double AgeMinMinutes = 25, AgeMaxMinutes = 40;
    public const double MaxBotSpread = 2;            // slowest bot's charter time / fastest bot's
    public const int RunMinutes = 60;                // how long a bot plays at most
    // ---- Rules v2 ----
    public const double MaxTrackShare = 0.4;         // no track is more than this share of any bot's purchases
    public static readonly ShopItemKind[] EveryKind = { ShopItemKind.Villager, ShopItemKind.Pace, ShopItemKind.Load, ShopItemKind.Pave, ShopItemKind.Building, ShopItemKind.Tools, ShopItemKind.Value, ShopItemKind.Extend, ShopItemKind.BellTrack };
    public const double MaxMapGap = 210;             // seconds between new things on the map after minute 2
    // Rules v2.1, the lead's decision: a run has only about a dozen requests and one bell comparison, so these two targets are
    // noisy for a single run. They are measured on the AVERAGE over seeds SeedFrom..SeedTo (per bot), and each single run only
    // has to stay inside a wide sanity band. Every other target stays per run.
    public const double BellMin = 0.10, BellMax = 0.25; // bell rung whenever ready: extra coin in 20 minutes (average over the seeds)
    public const double BellRunMin = 0.05, BellRunMax = 0.35; // sanity band for any single run
    public const double NoPaveSlower = 0.15;         // a bot that never paves is at least this much slower to the charter
    public const double RequestsMetMin = 0.5, RequestsMetMax = 0.8; // share met by a normal bot that rings the bell (average over the seeds)
    public const double RequestsRunMin = 0.3, RequestsRunMax = 0.95; // sanity band for any single run
    public const int SeedFrom = 1, SeedTo = 10;
}
