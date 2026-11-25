namespace TideToll.Sim.Town;

/// <summary>
/// Every number of the first age in one place. The balancer tunes these and nothing else.
/// Times are game seconds, distances are map cells, money is coin.
/// </summary>
public static class TownRules
{
    // ---- Clock ----
    public const int TickHz = 10;
    public const double Dt = 1.0 / TickHz;

    // ---- Walking ----
    public const double BaseSpeed = 2.2;            // cells per second on a worn-in track
    public const double TrailSpeedFactor = 0.6;     // speed factor of a brand-new faint trail (wear 0)
    public const double GravelSpeedFactor = 1.25;
    public const double CobbleSpeedFactor = 1.5;
    public const double PassesToWearIn = 6;         // full-length walks that wear a new road in
    public const double ComfortPerCellTrack = 0.22; // comfortable walkers per cell of road, by surface
    public const double ComfortPerCellGravel = 0.35;
    public const double ComfortPerCellCobbles = 0.5;
    public const double MinComfort = 2;
    public const double CrowdSlowdown = 0.6;        // speed = 1 / (1 + k * excess / comfort)
    public const double CrowdMinFactor = 0.35;      // never slower than this share of free speed
    public const double QueueSpacing = 0.8;         // cells between people waiting at a workplace

    // ---- Goods and jobs ----
    public const double WaterValue = 3;
    public const double GrainHomeValue = 6;
    public const double GrainMarketValue = 11;
    public const double FlourValue = 34;
    public const double WellWork = 1.5;             // seconds at the well
    public const double FieldWork = 3.0;
    public const double MillWork = 3.0;
    public const int WellPlaces = 1;
    public const int SecondBucketPlaces = 1;
    public const int FieldPlaces = 2;
    public const int MillPlaces = 2;

    // ---- Housing ----
    public const int PerCottage = 2;

    // ---- Shop: price = base * growth ^ level ----
    public const double VillagerBase = 14, VillagerGrowth = 1.5;
    public const double PaceBase = 6, PaceGrowth = 1.3;
    public const double PaceStep = 0.05;            // +5 % walking speed per level
    public const int PaceMax = 60;
    public static readonly double[] LoadPrices = { 160, 900, 3600 };
    public static readonly string[] LoadNames = { "Basket", "Yoke", "Handcart" };
    public const double GravelPerCell = 2.0;
    public const double CobblesPerCell = 6.0;
    public const double PaveGrowth = 1.12;          // each road paved makes the next 12 % dearer per cell
    public const int CobblesFromLevel = 8;          // cobbles are offered from this level on
    // Building offers (price), by offer id in TownPlan.Offers order.
    public const double SecondBucketPrice = 70;
    public const double MarketStallPrice = 260;
    public const double SecondFieldPrice = 700;
    public const double MillPrice = 1900;

    // ---- Renown and levels ----
    public const double RenownPerCoin = 1.0;        // renown of a purchase = price * this (at least 1)
    /// <summary>Renown needed to reach level i+1 (index 0 is level 1, always 0). Twelve levels; the last grants the charter.</summary>
    public static readonly double[] LevelRenown = { 0, 40, 120, 260, 520, 950, 1600, 2600, 4100, 6200, 9000, 13000 };

    // ---- The bell ----
    public const double BellHurry = 1.5;            // speed factor while ringing
    public const double BellDuration = 6;
    public const double BellRest = 30;

    // ---- Perks: every PerkEvery levels of a track ----
    public const int PerkEvery = 5;
    public const double VillagerPerkCoin = 0.05;    // "Good neighbours": +5 % coin per perk
    public const double PacePerkBell = 0.1;         // "Light feet": bell rests 10 % shorter per perk (floor 50 %)
    public const double PavePerkDiscount = 0.1;     // "Mason's eye": paving 10 % cheaper per perk (floor 50 %)

    // ---- Coin per minute ----
    public const int RateWindowSeconds = 60;
}
