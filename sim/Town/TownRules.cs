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
    public static double BaseSpeed = 2.5;            // cells per second on a worn-in track (seed 1 tuned: docs/plans/town-pacing.md)
    public static double TrailSpeedFactor = 0.7;     // speed factor of a brand-new faint trail (wear 0)
    public static double GravelSpeedFactor = 1.25;
    public static double CobbleSpeedFactor = 1.5;
    public static double PassesToWearIn = 6;         // full-length walks that wear a new road in
    public static double ComfortPerCellTrack = 0.22; // comfortable walkers per cell of road, by surface
    public static double ComfortPerCellGravel = 0.35;
    public static double ComfortPerCellCobbles = 0.5;
    public static double MinComfort = 2;
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
    public static double VillagerBase = 5, VillagerGrowth = 1.45;
    public static double PaceBase = 2, PaceGrowth = 1.035;
    public static double PaceCap = 1.5;            // Pace adds at most +150 % walking speed: factor = 1 + cap * (1 - decay ^ level)
    public static double PaceDecay = 0.98;         // so the first level adds 3 %, later ones less and less
    public static int PaceMax = 1000;
    public static readonly double[] LoadPrices = { 300, 1500, 5000 };
    public static readonly string[] LoadNames = { "Basket", "Yoke", "Handcart" };
    public static double GravelPerCell = 2.0;
    public static double CobblesPerCell = 6.0;
    public static double PaveGrowth = 1.04;          // each road paved makes the next 4 % dearer per cell
    public static int CobblesFromLevel = 8;          // cobbles are offered from this level on
    // Building offers (price), by offer id in TownPlan.Offers order.
    public static double SecondBucketPrice = 70;
    public static double MarketStallPrice = 150;
    public static double SecondFieldPrice = 400;
    public static double MillPrice = 900;

    // ---- Renown and levels ----
    public static double RenownPerCoin = 1.0;        // renown of a purchase = price * this (at least 1)
    /// <summary>Renown needed to reach level i+1 (index 0 is level 1, always 0). Twelve levels; the last grants the charter.</summary>
    public static readonly double[] LevelRenown = { 0, 30, 80, 180, 350, 700, 1700, 3600, 7000, 12500, 22000, 35000 };

    // ---- The bell ----
    public static double BellHurry = 1.5;            // speed factor while ringing
    public static double BellDuration = 6;
    public static double BellRest = 30;

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
}
