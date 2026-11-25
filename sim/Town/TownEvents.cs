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
