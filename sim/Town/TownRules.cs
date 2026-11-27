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
    // Rules v3: goods pay less (were 2, 3, 4, 10) and the first prices are lower (Villager 4.5, Pace 1.8; were 5 and 2), because
    // boons, milestones, extra hands and the drive made the town faster.
    public static double WaterValue = 1.8;
    public static double GrainHomeValue = 2.4;
    public static double GrainMarketValue = 3.2;
    public static double FlourValue = 6.5;
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
    public static double VillagerBase = 4.5, VillagerGrowth = 1.32;   // rules v3: was 1.3
    public static double PaceBase = 1.8, PaceGrowth = 1.4;   // rules v3: was 1.36
    public static double PaceStep = 0.03;          // rules v2.2: each Pace level adds 3 % walking speed (v2.1: 4 %, v2: 5 %) (factor = 1 + step * level)
    public static int PaceMax = 27;                // rules v2.1: Pace is finished after 25 levels (v2: 20)
    public static readonly double[] LoadPrices = { 200, 1600, 4000 };   // rules v3: Basket 200 (was 250)   // rules v2.2: Basket 250 (was 300), so the payback bot's wait for it no longer empties minutes 8 to 9
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
    public static double ToolsBase = 30, ToolsGrowth = 1.38;   // rules v3: was 1.33
    public static int ToolsMax = 20, ToolsFromLevel = 3;
    public static double ToolsStep = 0.03;          // work time = base / (1 + step * level)
    // Value tracks, by Good (index 0 unused): Water, Grain, Flour pay ValueStep more per unit per level.
    public static readonly double[] ValueBase = { 0, 12, 30, 250 };
    public static readonly double[] ValueGrowth = { 0, 1.55, 1.43, 1.17 };
    public static int ValueMax = 16;
    public static double ValueStep = 0.04;
    // Extending a cottage: each cottage twice, each extension houses one more villager.
    public static int ExtendMax = 2, ExtendFromLevel = 5;
    public static double ExtendBase = 40, ExtendGrowth = 1.33;   // rules v2.2: base 40 (was 60): more cheap steps at levels 5 to 7
    // The bell track: each level adds BellTrackHurry seconds of hurry and takes BellTrackRest seconds off the rest.
    public static double BellTrackBase = 60, BellTrackGrowth = 2.6;
    public static int BellTrackMax = 5;
    public static double BellTrackHurry = 0.4, BellTrackRest = 1;   // rules v2.2: was 0.6 and 2, so the base bell carries more of its worth

    // ---- Rules v2.1: steps on sale by town level (index 0 = level 1; the number of steps of the track that can have been
    // bought by then). Few steps early, more at later levels, the last ones near the end of the age, so no track is bought
    // out early and every level-up brings new steps. A step not yet on sale is listed with "Unlocks at level N". ----
    public static readonly int[] PaceAt = { 4, 6, 8, 10, 12, 13, 14, 15, 16, 17, 18, 20, 22, 24, 27, 27 };   // green pass: more at levels 13 to 16 (max 27, was 25)
    public static readonly int[] ToolsAt = { 0, 0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 20, 20 };   // green pass: max 20 (was 18)
    public static readonly int[] ValueAt = { 2, 3, 4, 5, 6, 7, 8, 9, 9, 10, 10, 11, 12, 13, 16, 16 };   // green pass: max 16 (was 14)   // rules v3: one more step at levels 7 to 13
    public static readonly int[] FlourValueAt = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 16, 16 };
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
    public static double RequestReward = 0.4;       // reward coin = amount * unit value * this; renown = the reward coin (rules v2.2: was 0.6; more requests are met now)
    public static int RequestMinAmount = 6;
    public static double RequestCapacityShare = 0.95; // and at most this share of what its workplaces could turn out
    // Rules v2.1: requests adapt to the player. Each request met multiplies the next amount by RequestGrowMet, each missed
    // by RequestShrinkMissed, within RequestScaleMin..Max, so any player ends up meeting about ln(1/shrink) / ln(grow/shrink)
    // of them (0.65 with 1.10 and 0.84). Saved with the town (TownState.ReqScale).
    public static double RequestGrowMet = 1.10, RequestShrinkMissed = 0.84;
    public static double RequestScaleMin = 0.3, RequestScaleMax = 2.5;
    // Rules v2.2, the lead's decision: requests start easy and grow. The first requests of a town are small enough that
    // almost any player meets them (amount times RequestEaseIn[request number], the last value from then on), and the
    // adaptation takes it from there.
    public static readonly double[] RequestEaseIn = { 0.4, 0.6, 0.8, 1.0 };

    // ---- Renown and levels ----
    public static double RenownPerCoin = 1.0;        // renown of a purchase = price * this (at least 1)
    /// <summary>Renown needed to reach level i+1 (index 0 is level 1, always 0). Sixteen levels (rules v2); the last grants the charter.</summary>
    // Rules v2.2: the last two levels need more (62000 and 102000, were 59000 and 84000), so the saver reaches the charter after
    // 25 minutes on every seed (the lead's decision); more than about 104000 empties the shop before the charter.
    public static readonly double[] LevelRenown = { 0, 20, 100, 300, 700, 1300, 2200, 3500, 5700, 9500, 15000, 22500, 33000, 52000, 84000, 160000 };   // rules v3: levels 9 to 16 need more; the charter drive pays 65 % of what is left at level 15

    // ---- The bell ----
    // Rules v2.2: a stronger, longer ring (were 1.17 and 6 s). Measured on frozen towns (TownPacing.BellWorth) the old bell was
    // worth about 6 %; this one about 15 % on average over seeds 1 to 10.
    public static double BellHurry = 1.4;            // speed factor while ringing
    public static double BellDuration = 8;
    public static double BellRest = 40;

    // ---- Villagers as people (n7 step 1) ----
    // A villager's own kind of work (Town.LeaningOf, from the seed and its number) counts this much more when it chooses a job.
    public static double LeanBonus = 0.10;

    // ---- Perks: every PerkEvery levels of a track ----
    public const int PerkEvery = 5;
    public static double VillagerPerkCoin = 0.05;    // "Good neighbours": +5 % coin per perk
    public static double PacePerkBell = 0.1;         // "Light feet": bell rests 10 % shorter per perk (floor 50 %)
    public static double PavePerkComfort = 0.2;      // "Mason's eye": paved roads carry 20 % more walkers per perk

    // ---- Coin per minute ----
    public const int RateWindowSeconds = 60;

    // ---- Rules v3 (docs/plans/town-rules3-progress.md) ----
    // Choices of two: at each of these levels the town picks one of two boons for good (Boon pairs 0/1, 2/3, 4/5, 6/7: lanes or backs, well or rows, room or hands, market or guild).
    public static readonly int[] ChoiceLevels = { 4, 7, 10, 13 };
    public static double BoonWellWork = 0.2;       // Deeper well: well work this much faster (time / (1 + x))
    public static double BoonFieldWork = 0.6;       // Longer field rows: field work this much faster
    public static double BoonLanes = 0.5;           // Wider lanes: every road carries this much more before crowding
    public static double BoonWalk = 0.045;           // Stronger backs: everyone walks this much faster
    public static int BoonRoom = 1;                 // A third room: every cottage houses this many more
    public static double BoonHands = 0.4;           // Faster hands: all work this much faster
    public static double BoonGrain = 0.12;          // Market right: Grain pays this much more
    public static double BoonFlour = 0.2;         // Miller's guild: Flour pays this much more
    // Milestones: every MilestoneEvery-th step of Pace, Tools and each value track adds this on top of the step.
    public const int MilestoneEvery = 5;
    public static double PaceMilestone = 0.02, ToolsMilestone = 0.04, ValueMilestone = 0.04;
    // The charter drive: asked when level MaxLevel - 1 is reached. Amount = the town's whole coin rate for DriveMinutes,
    // in units of its best good (at most DriveCapacityShare of what its workplaces can make in that time); reward = DriveShare
    // of the renown still needed for the charter, paid in parts as units arrive. The good pays DriveBonus more while it runs.
    // Green pass: the charter needs at least this many minutes of the town's best coin rate above the renown held at level 15.
    public static double CharterMinutes = 10;
    public static double DriveMinutes = 1.0, DriveCapacityShare = 0.8, DriveShare = 0.65, DriveBonus = 1.25;
    // Extra hands: a queue of HandQueueLength or more for HandQueueSeconds offers one more place there, HandMax per workplace.
    public static int HandQueueLength = 2, HandMax = 2;
    public static double HandQueueSeconds = 30;
    // ---- Green pass: small touches (sim/Town/TownTouches.cs) ----
    // A touch appears each time the coin earned passes a beat; the next beat is TouchMinutes of the town's income at that
    // moment (at least TouchRateMin a minute) further on, so touches come about every TouchMinutes and follow the town.
    public static double TouchMinutes = 3.5, TouchMinutesRiver = 1.5, TouchRateMin = 30, TouchFirstCoin = 40, TouchMaxGap = 170;
    public const int TouchMax = 44, TouchSpotMax = 64, TouchesPerPlot = 3;   // at most TouchMax touches, on up to TouchSpotMax reserved spots
    // ---- Green pass 2: Regulars, the first age's ladder of small steps (sim/Town/TownRegulars.cs) ----
    // Each regular: every delivery pays RegularsStep more. Price: RegularsMinutes of the best coin rate when the level was
    // reached (at least RegularsRateMin a minute), times RegularsGrowth ^ regulars won at this level; starts again each level.
    public static double RegularsStep = 0.004, RegularsMinutes = 0.15, RegularsGrowth = 1.35, RegularsRateMin = 20;
    public static double HandWellBase = 150, HandFieldBase = 600, HandMillBase = 4000, HandGrowth = 2.5;
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
    public const double BellMin = 0.10, BellMax = 0.25; // bell rung whenever ready: extra delivery coin (average; rules v2.2: over the frozen-town comparisons below)
    public const double BellRunMin = 0.05, BellRunMax = 0.35; // sanity band for any single run
    public const double NoPaveSlower = 0.15;         // a bot that never paves is at least this much slower to the charter
    // Rules v2.2, the lead's decision: requests start easy and grow (TownRules.RequestEaseIn), the adaptation takes it from
    // there; aim for the middle of this band (about two thirds), not the floor.
    public const double RequestsMetMin = 0.5, RequestsMetMax = 0.8; // share met by a normal bot that rings the bell (average over the seeds)
    public const double RequestsRunMin = 0.3, RequestsRunMax = 0.95; // sanity band for any single run
    public const int SeedFrom = 1, SeedTo = 10;
    // ---- Rules v2.2, the lead's decisions ----
    // The saver is out of the purchases-per-minute floor (it waits on purpose, so the floor measures nothing for it). Its own
    // check: after minute 2 it never goes longer than this without a purchase (and, as before, it buys every kind, every Load
    // step and every building before the charter and reaches the charter in AgeMinMinutes..AgeMaxMinutes).
    public const double SaverMaxPurchaseGap = 90;
    // The bell is measured on frozen towns (TownPacing.BellWorth): the payback bot's town at these minutes, run this long with
    // no purchases, with and without the bell. BellMin..BellMax is the average over all comparisons on seeds SeedFrom..SeedTo,
    // BellRunMin..BellRunMax holds for every single comparison. Two free runs buy different things and that noise swamped the bell.
    public static readonly int[] BellAtMinutes = { 10, 20 };
    public const double BellFrozenMinutes = 5;
}
