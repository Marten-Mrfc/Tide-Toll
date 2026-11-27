namespace TideToll.Sim.Town;

public enum TownEventKind : byte
{
    Delivery = 0,          // Villager, Good, Amount, Coin, X, Y (map cells), Plot (where delivered)
    Purchase = 1,          // Item (ShopItemKind), Index (road id for Pave, offer id for buildings), Coin (price), Value (renown added), Level (new track level)
    LevelReached = 2,      // Level; Text = the gift in words; the gift itself: TownView.Level(n).Plots / Roads / Offers
    RoadWornIn = 3,        // Index = road id
    RoadPaved = 4,         // Index = road id, Surface
    OfferAvailable = 5,    // Item, Index (offer id, or -1 for a track), Text = name
    PerkEarned = 6,        // Item (track), Level (track level), Text = perk name
    AchievementUnlocked = 7, // Index = achievement index, Text = name
    BellRung = 8,          // Value = seconds of hurry
    VillagerArrived = 9,   // Villager, Plot (home), X, Y
    BuildingOpened = 10,   // Index = offer id, Plot
    Charter = 11,          // the age is complete; RingNextAge() is now possible
    NextAge = 12,          // RingNextAge() was called (the second age is not built yet)
    // Added in rules v2
    RequestAsked = 13,     // Good, Amount (asked), Coin (reward), Value (seconds allowed), Index (who: TownRequests.Who), Text (the ask in words)
    RequestMet = 14,       // Good, Amount, Coin (reward paid), Value (renown added), Text
    RequestMissed = 15,    // Good, Amount (asked), Level (delivered by the end), Text
    CottageExtended = 16,  // Plot (cottage), Level (its extensions now, 1 or 2), X, Y (door)
    // Added in rules v3
    ChoiceOffered = 17,    // Index = choice (0..3), Level = town level; the two boons: Town.Choice
    BoonChosen = 18,       // Index = (int)Boon taken, Level = choice (0..3), Text = boon name
    Milestone = 19,        // Item (Pace, Tools or Value), Index ((int)Good for Value, else -1), Level (track level), Text = milestone name
    DriveAsked = 20,       // Good, Amount, Value (renown reward in all); Town.Drive for the rest
    DriveProgress = 21,    // Good, Amount (delivered so far), Value (renown added by this delivery)
    DriveMet = 22,         // Good, Amount, Value (renown paid in all)
    // Added in the second age (docs/plans/town-sim-api.md "The second age")
    BoatArrived = 23,      // Index = boat id, Good (wanted), Amount (wanted), Level = (int)BoatKind, Value = 1 for a skipper's request, X, Y
    BoatFilled = 24,       // Index = boat id, Good, Amount (loaded), Coin (bonus paid), Value = 1 for a request, X, Y
    BoatLeft = 25,         // Index = boat id, Good, Amount (loaded), Level (wanted): its patience ran out, X, Y
    TollPaid = 26,         // Coin, X, Y (the drawbridge)
    FerryDocked = 27,      // Index = bank (0 near, 1 far), X, Y
    DrawbridgeRaised = 28, // X, Y
    DrawbridgeLowered = 29,// X, Y
    Seal = 30,             // the borough seal: the second age is complete
    LandingDelivery = 31,  // Villager, Good, Amount, Coin, X, Y (the landing), Index = boat id (-1: sold to the landing store)
    // Added in the green pass
    TouchAdded = 32,       // Index = touch number (Town.Touch(Index)), Level = (int)TouchKind, Plot (first-age plot, -1 if a river plot), Amount = river plot (-1 if first age), X, Y, Value = heading (radians), Text = kind name
    // Added with trades (n7 step 2)
    TradeChanged = 33,     // Villager, Index = (int)Trade now, Amount = (int)Trade before, Level = 1 by the player (0 by the town), X, Y
}

/// <summary>One thing that happened during a step. Drain them with Town.DrainEvents each frame.</summary>
public struct TownEvent
{
    public TownEventKind Kind;
    public double Time;     // game seconds
    public int Villager;
    public Good Good;
    public int Amount;
    public double Coin;
    public double Value;
    public double X, Y;
    public int Plot;
    public int Index;
    public int Level;
    public ShopItemKind Item;
    public Surface Surface;
    public string? Text;
}
