using System;

namespace TideToll.Sim;

/// <summary>
/// The river rules of Prototype 1 step 2 (design doc, Gameplay tab, "River and water" and "Economy"): boats
/// on a schedule, crossings with a clearance, money. Off unless <see cref="SimParams.River"/> is set, so a
/// world made the old way runs exactly as before. Values marked "chosen" are the implementation spec's,
/// not the design doc's. Times are sim seconds at 1x (1 day = 15 s).
/// Tuned 25 Nov 2026 with the strategy bot (README "Tuning"): BoatValueMul 2 to 3, StartMoney 120 to 0, DrawbridgePrice 240 to 200,
/// KeeperPrice 30 to 120, FerryCapacity 6 to 5, FerryTurnSec 0.5 to 0.75, FerryUpkeep 2 to 5, FerryWage 3 to 15.
/// </summary>
public sealed record RiverRules
{
    public static readonly RiverRules Default = new();

    /// <summary>Walkers carry grain from the field to the landing (adds the Landing building). Off: no goods, no landing building.</summary>
    public bool HaulGoods { get; init; } = true;

    // Boats. Index 0 = rowboat, 1 = barge (BoatKind).
    public double SailRowsPerSec { get; init; } = 10;           // chosen
    public int RowboatMast { get; init; } = 0;
    public int BargeMast { get; init; } = 2;
    public int RowboatCapacity { get; init; } = 12;
    public int BargeCapacity { get; init; } = 40;
    /// <summary>Mean gap between arrivals at reputation 50: rowboat 1 per 2 days, barge 1 per day.</summary>
    public double RowboatGapSec { get; init; } = 30;
    public double BargeGapSec { get; init; } = 15;
    public double GapJitter { get; init; } = 0.25;               // chosen, plus or minus
    public double MooringSec { get; init; } = 15;               // 1 day
    public double PatienceSec { get; init; } = 15;              // 1 day
    public int Berths { get; init; } = 2;                        // chosen
    /// <summary>Boats stop this many rows below a crossing they cannot pass, and keep this spacing in a queue.</summary>
    public double StopRows { get; init; } = 2;
    public int MaxBoats { get; init; } = 32;
    public int LandingMinRow { get; init; } = 14;
    public int LandingMaxRow { get; init; } = 108;

    // Reputation (0 to 100): frequency x(0.5 + rep / 100).
    public double ReputationStart { get; init; } = 50;
    public double ReputationServed { get; init; } = 1;
    public double ReputationTurnedBack { get; init; } = 5;

    // Goods and prices.
    public double GrainPrice { get; init; } = 2;
    /// <summary>Boat price multiplier (chosen 2, flag A: with the doc's 70% market fallback a lost boat would cost only 30%).</summary>
    public double BoatValueMul { get; init; } = 3;
    public double MarketShare { get; init; } = 0.7;
    /// <summary>The village market buys at most this many units per household per day (doc, Income sources); the rest goes unsold.</summary>
    public double MarketUnitsPerHouseDay { get; init; } = 1;

    // Crossings.
    public double FootbridgePrice { get; init; } = 50;
    public double FerryPrice { get; init; } = 40;
    /// <summary>200c + 40 stone, with the stone folded into coin (chosen).</summary>
    public double DrawbridgePrice { get; init; } = 200;
    public double KeeperPrice { get; init; } = 120;              // chosen (tuned 25 Nov: doc has none)
    public double FerryUpkeep { get; init; } = 5;               // per season
    public double FerryWage { get; init; } = 15;
    public double DrawbridgeUpkeep { get; init; } = 4;
    public double KeeperWage { get; init; } = 5;
    public int FootbridgeClearance { get; init; } = 1;
    public int DrawbridgeClosedClearance { get; init; } = 1;
    public int FerryCapacity { get; init; } = 5;
    public double FerryLoadSec { get; init; } = 0.5;             // chosen
    public double FerryCrossSec { get; init; } = 1.0;            // chosen
    /// <summary>
    /// Unloading and turning round at a stage before boarding starts. Chosen so that a full ferry (6 aboard, 1.0 s across)
    /// carries the doc's "about 30 each way per boat" a day: 6 per leg, a leg every 1.0 + 0.5 s, so 15 s x 6 / 3 s = 30.
    /// </summary>
    public double FerryTurnSec { get; init; } = 0.75;
    /// <summary>Walker capacity per day of a 1-cell footbridge and of a closed drawbridge (doc, crossing table: about 120 and about 250).</summary>
    public double FootbridgeWalkersPerDay { get; init; } = 120;
    public double DrawbridgeWalkersPerDay { get; init; } = 250;
    /// <summary>A bridge's bucket holds this many seconds of its capacity, so a small group crosses together (chosen).</summary>
    public double BridgeBurstSec { get; init; } = 1;
    /// <summary>Expected wait for loading and half the headway, in the ferry's routing cost (spec: 0.75 s).</summary>
    public double FerryExpectedWaitSec { get; init; } = 0.75;
    /// <summary>Drawbridge cycle, half the doc's 2 s / 3 s per boat / 2 s (flag B).</summary>
    public double RaiseSec { get; init; } = 1.0;
    public double PerBoatSec { get; init; } = 1.5;
    public double LowerSec { get; init; } = 1.0;
    /// <summary>A manual raise with no boat to pass lowers itself after this long.</summary>
    public double ManualMaxRaisedSec { get; init; } = 8;
    /// <summary>The bridge stays raised while a masted boat is within this many rows below it.</summary>
    public double HoldRowsBelow { get; init; } = 4;
    public double KeeperSeeRows { get; init; } = 6;
    public double KeeperDelaySec { get; init; } = 1;
    /// <summary>Learning rate of a crossing's average walker wait, per served walker.</summary>
    public double WaitAlpha { get; init; } = 0.2;
    /// <summary>A walker that has waited this long gives up and goes home (a safety net; queues normally clear long before).</summary>
    public double MaxWalkerWaitSec { get; init; } = 40;

    // Spring high water (doc, "Spring high water" and "Seasons and weather"). On with the river rules.
    public bool SpringFlood { get; init; } = true;
    /// <summary>Chance of a flood on the first day of each spring (doc: Riverbend 70%; Delta Islands 90%, Mere 40%).</summary>
    public double FloodChance { get; init; } = 0.7;
    /// <summary>The band is under water for 1 or 2 days (doc), drawn evenly.</summary>
    public int FloodMinDays { get; init; } = 1;
    public int FloodMaxDays { get; init; } = 2;
    /// <summary>Width of the flood band beyond the water's edge on each bank, in cells (chosen: it stops one cell short of the nearest building the map generator allows).</summary>
    public double FloodBandCells { get; init; } = 4;
    /// <summary>Walking through the flooded band, as through the ford (chosen: the doc's ford speed 0.4 and the sim's ford cost x3).</summary>
    public double FloodWadeSpeedMul { get; init; } = 0.4;
    public double FloodWadeCostMul { get; init; } = 3;
    /// <summary>Mud on the band after the flood, by ground state (doc, starting numbers: trail x1.6, track x1.7, hollow way x1.8).</summary>
    public double MudTrailMul { get; init; } = 1.6;
    public double MudTrackMul { get; init; } = 1.7;
    public double MudHollowMul { get; init; } = 1.8;
    /// <summary>Repairs per building in the band per flood (doc, Costs: 10c per building per flood). The landing is exempt.</summary>
    public double FloodRepairCost { get; init; } = 10;

    // Winter ice (doc, "Winter ice").
    public bool WinterIce { get; init; } = true;
    /// <summary>Chance per winter that the river freezes on its second day, until the first day of spring (doc: Hamlet 50%).</summary>
    public double IceChance { get; init; } = 0.5;
    /// <summary>Ice-wear gain per footstep relative to the wear grid's (doc: double).</summary>
    public double IceWearMul { get; init; } = 2;
    /// <summary>Cost and speed on the ice (chosen: like grass; the doc's snow, +20% in winter, is weather and not built).</summary>
    public double IceCostMul { get; init; } = 1;
    public double IceSpeedMul { get; init; } = 1;

    // Money.
    public double StartMoney { get; init; } = 0;
    /// <summary>Upkeep may take the purse down to this; purchases need the full price in hand.</summary>
    public double DebtLimit { get; init; } = -100;
    public double DebtInterest { get; init; } = 0.1;            // per season on a negative purse
    public double HearthTax { get; init; } = 1;                 // per house per season
    public double EraseCostShare { get; init; } = 0.5;          // chosen (the doc's 2x would block bot experiments)

    internal int Mast(BoatKind k) => k == BoatKind.Rowboat ? RowboatMast : BargeMast;
    internal int Capacity(BoatKind k) => k == BoatKind.Rowboat ? RowboatCapacity : BargeCapacity;
    internal double Gap(BoatKind k) => k == BoatKind.Rowboat ? RowboatGapSec : BargeGapSec;

    /// <summary>Build price of a crossing type.</summary>
    public double Price(CrossingType t) => t switch
    {
        CrossingType.Footbridge => FootbridgePrice,
        CrossingType.Ferry => FerryPrice,
        CrossingType.Drawbridge => DrawbridgePrice,
        _ => 0,
    };

    /// <summary>Boat arrivals per day of one kind at a reputation.</summary>
    public double ArrivalsPerDay(BoatKind k, double reputation, double daySec)
        => daySec * (0.5 + reputation / 100) / Gap(k);
}

public enum BoatKind : byte { Rowboat = 0, Barge = 1 }

/// <summary>Sailing north to the landing, Waiting (blocked; patience runs), Mooring, Leaving (served, northbound), TurnedBack (southbound, gone).</summary>
public enum BoatState : byte { Sailing = 0, Waiting = 1, Mooring = 2, Leaving = 3, TurnedBack = 4 }

public enum CrossingType : byte { Ford = 0, Footbridge = 1, Ferry = 2, Drawbridge = 3 }

/// <summary>Drawbridge leaf: Closed (walkers cross, masts blocked), Raising, Raised (boats pass, walkers wait), Lowering.</summary>
public enum BridgeState : byte { Closed = 0, Raising = 1, Raised = 2, Lowering = 3 }

/// <summary>What a walker is doing at a crossing: nothing, waiting on the bank, or riding the ferry.</summary>
public enum WalkerHold : byte { None = 0, Waiting = 1, Riding = 2 }

/// <summary>One boat on the river. Y is the row of its bow in cell units (it sails along the river centre, RiverX[row]).</summary>
public struct Boat
{
    public int Id;
    public BoatKind Kind;
    public BoatState State;
    public byte Mast;
    /// <summary>This tick's and the previous tick's row (interpolate for drawing); grows to the south.</summary>
    public double Y, PrevY;
    /// <summary>-1 northbound, +1 southbound.</summary>
    public int Dir;
    public int Cargo, Capacity;
    /// <summary>Coin owed for goods bought; booked when the boat leaves its berth.</summary>
    public double Earned;
    public double PatienceLeft, MoorLeft;
    /// <summary>Has moored and bought: never turns back.</summary>
    public bool Served;
    /// <summary>Crossing id it is waiting at (-1 none, -2 for a berth), while Waiting.</summary>
    public int WaitingAt;
    /// <summary>Crossing id it has been let through (a raised drawbridge), -1 none.</summary>
    internal int Through;
    public int Berth;
}

public enum RiverEventKind : byte
{
    BoatArrived, BoatWaiting, BoatPassed, BoatMoored, BoatSold, BoatTurnedBack, BoatLeft,
    BridgeRaising, BridgeRaised, BridgeLowering, BridgeLowered,
    FerryDeparted, FerryLanded,
    CannotAfford, SeasonSettled, KeeperHired, CrossingBuilt, CrossingRemoved,
    FloodRising, FordClosed, FloodReceded, FordOpen, FloodRepairs, RiverFrozen, Thaw,
}

/// <summary>
/// One thing that happened on the river this tick. Value is coin for BoatSold, CannotAfford, SeasonSettled (net) and
/// FloodRepairs, passengers for the ferry, the flood's length in days for FloodRising. FordClosed and FordOpen name the
/// ford (crossing 0).
/// </summary>
public readonly struct RiverEvent
{
    public readonly RiverEventKind Kind;
    public readonly long Tick;
    public readonly int Boat, Crossing;
    public readonly double Value;

    public RiverEvent(RiverEventKind kind, long tick, int boat, int crossing, double value)
    {
        Kind = kind; Tick = tick; Boat = boat; Crossing = crossing; Value = value;
    }

    public override string ToString() => $"{Tick} {Kind} boat {Boat} crossing {Crossing} {Value:0.##}";
}

/// <summary>The answer to a river command: Ok, or a reason in words for the player.</summary>
public readonly struct RiverResult
{
    public bool Ok { get; }
    public string Reason { get; }
    /// <summary>The new or affected crossing's id, -1 if none.</summary>
    public int Id { get; }

    private RiverResult(bool ok, string reason, int id) { Ok = ok; Reason = reason; Id = id; }
    public static RiverResult Success(int id, string what = "") => new(true, what, id);
    public static RiverResult Refused(string reason) => new(false, reason, -1);
    public override string ToString() => Ok ? $"Ok ({Id})" : Reason;
}
