using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-town: the town through real input (mouse and key events parsed by Godot, reaching the interface first).
/// Buys the first upgrades by clicking their bookmarks and by key, then plays at high speed by clicking whatever is
/// affordable until at least level 3, checking that every purchase changed the town (a villager more and drawn, a pace
/// or load level, a building drawn), that each level's gift arrived and was drawn (its plots popped in, its roads drew
/// themselves in, a margin note said so), that a road wore in visibly, that paving by clicking a road works (Pave mode
/// lights the roads, the hovered road shows a price, the click lays gravel that is drawn), and that the bell works by
/// key and by click. Villagers never jump or slide sideways at normal speed.
/// </summary>
public partial class TownTest : TownTestBase
{
    protected override string Marker => "TOWN PASS";
    private int _bought, _gifts, _villagerBuys, _paceBuys, _loadBuys, _buildingBuys;

    protected override async Task Run()
    {
        // A new road is a faint trail once drawn in.
        int r0 = FirstOpenRoad();
        await Until(() => Main.Ground.RoadReveal(r0) >= 1f, 600, "the first road drawn in");
        await Frames(30);
        float faint = Main.Ground.RoadShown(r0);
        Check(faint > 0.15f && faint < TownGround.TrailWear + 0.2f, $"a new road is a faint trail (drawn wear {faint:0.00})");
        Notes.Add($"faint={faint:0.00}");

        // Purposeful villagers at 1x: no jumps, no sideways sliding.
        await WatchVillagers(240);

        // First upgrades by mouse: Pace, then a villager; then Pace by its key.
        Main.Driver.SetSpeed(4);
        await BuyByClick(ShopItemKind.Pace);
        await BuyByClick(ShopItemKind.Villager);
        await Until(() => Main.Hud.Mark(ShopItemKind.Pace) is { Disabled: false }, 60 * 60, "pace affordable again");
        int pace = T.PaceLevel;
        await Key(Godot.Key.Key2);
        Check(T.PaceLevel == pace + 1, "key 2 buys pace");
        _bought++;

        // Play fast with real clicks until at least level 3 and the first building bought, checking gifts, wear, paving and the bell on the way.
        Main.Driver.SetSpeed(32);
        bool wornSeen = false, paved = false, bellKey = false, bellClick = false;
        int level = T.Level;
        for (int f = 0; f < 60 * 600 && (T.Level < 3 || !wornSeen || !paved || !bellKey || !bellClick || _buildingBuys < 1); f++)
        {
            await Frames(1);
            if (T.Level > level)
            {
                for (int l = level + 1; l <= T.Level; l++) await CheckGift(l);
                level = T.Level;
            }
            if (!wornSeen && T.RoadInfo(r0).WornIn)
            {
                await Frames(90);
                float firm = Main.Ground.RoadShown(r0);
                Check(firm > faint + 0.3f, $"a worn-in road is drawn firmer ({faint:0.00} to {firm:0.00})");
                Notes.Add($"worn={firm:0.00}");
                wornSeen = true;
            }
            if (!paved && T.SuggestedPave() >= 0 && T.Coin >= T.PavePrice(T.SuggestedPave())) { await PaveByClick(); paved = true; continue; }
            if (!bellKey && T.BellReady) { await BellByKey(); bellKey = true; continue; }
            if (bellKey && !bellClick && T.BellReady) { await BellByClick(); bellClick = true; continue; }
            if (f % 3 == 0) await BuyCheapest();
        }
        Check(T.Level >= 3, $"reached level 3 (level {T.Level})");
        Check(wornSeen && paved && bellKey && bellClick, $"worn={wornSeen} paved={paved} bellKey={bellKey} bellClick={bellClick}");
        Check(_villagerBuys >= 2 && _paceBuys >= 2, $"bought villagers ({_villagerBuys}) and pace ({_paceBuys})");
        Check(_buildingBuys >= 1, $"bought a building ({_buildingBuys})");
        Notes.Add($"level={T.Level} bought={_bought} villagers={_villagerBuys} pace={_paceBuys} load={_loadBuys} buildings={_buildingBuys} gifts={_gifts} notes={Main.Hud.NotesShown} time={T.Time:0}s");
    }

    private int FirstOpenRoad()
    {
        for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r)) return r;
        throw new Exception("no open road at the start");
    }

    private async Task WatchVillagers(int frames)
    {
        Main.Driver.SetSpeed(1);
        int sideways = 0, moving = 0;
        float maxStep = 0;
        for (int i = 0; i < frames; i++)
        {
            await Frames(1);
            maxStep = Math.Max(maxStep, Main.Villagers.MaxStepCells);
            if (Main.Villagers.MaxStepCells > 0.002f) moving++;
            if (Main.Villagers.MaxSideways > 0.6f) sideways++;
        }
        Check(moving > frames / 4, $"villagers walk ({moving} frames moving)");
        Check(maxStep < 0.5f, $"no villager jumps at 1x (largest step {maxStep:0.00} cells)");
        Check(sideways <= moving / 10, $"villagers face the way they walk ({sideways} of {moving} frames turned over 0.6 rad)");
        Notes.Add($"maxstep={maxStep:0.000}");
    }

    private async Task BuyByClick(ShopItemKind kind)
    {
        await Until(() => Main.Hud.Mark(kind) is { Disabled: false }, 60 * 120, kind + " affordable");
        await ClickMark(kind);
    }

    /// <summary>Clicks a bookmark and checks the town changed the way that purchase should change it.</summary>
    private async Task ClickMark(ShopItemKind kind)
    {
        var b = Main.Hud.Mark(kind)!;
        long purchases = T.Purchases;
        int villagers = T.VillagerCount, pace = T.PaceLevel, load = T.LoadLevel, built = Main.Buildings.BuiltCount;
        await Click(TestInput.Centre(b));
        if (T.Purchases == purchases) return; // the price moved past the purse between frames: not a failure of the click
        _bought++;
        switch (kind)
        {
            case ShopItemKind.Villager:
                Check(T.VillagerCount == villagers + 1, "a villager purchase adds a villager");
                await Until(() => Main.Villagers.Drawn == T.VillagerCount, 30, "the new villager drawn");
                _villagerBuys++;
                break;
            case ShopItemKind.Pace: Check(T.PaceLevel == pace + 1, "a pace purchase raises pace"); _paceBuys++; break;
            case ShopItemKind.Load: Check(T.LoadLevel == load + 1, "a load purchase raises load"); _loadBuys++; break;
            case ShopItemKind.Building:
                await Until(() => Main.Buildings.BuiltCount > built, 60, "the bought building drawn");
                _buildingBuys++;
                break;
        }
    }

    private async Task BuyCheapest()
    {
        int best = -1;
        var items = Main.Hud.ShownItems;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.Kind == ShopItemKind.Pave || !it.Affordable) continue;
            // A sensible player: a villager or a building whenever one is affordable, else the cheapest.
            int rank = it.Kind is ShopItemKind.Villager or ShopItemKind.Building ? 0 : 1;
            int bestRank = best < 0 ? 9 : items[best].Kind is ShopItemKind.Villager or ShopItemKind.Building ? 0 : 1;
            if (best < 0 || rank < bestRank || (rank == bestRank && it.Price < items[best].Price)) best = i;
        }
        if (best < 0) return;
        await ClickMark(items[best].Kind);
    }

    private async Task CheckGift(int level)
    {
        var def = T.Plan.Level(level);
        foreach (int p in def.Plots)
        {
            var plot = T.Plan.Plots[p];
            if (plot.Offer >= 0) await Until(() => Main.Buildings.PlotStaked(p), 30, $"level {level}: offer plot {p} staked");
            else await Until(() => Main.Buildings.PlotDrawn(p), 30, $"level {level}: plot {p} drawn");
        }
        foreach (int r in def.Roads)
        {
            await Until(() => Main.Ground.RoadReveal(r) >= 1f, 1200, $"level {level}: road {r} drew itself in");
            await Until(() => Main.Ground.RoadShown(r) > 0.15f, 120, $"level {level}: road {r} drawn");
        }
        bool noted = false;
        foreach (var n in Main.Hud.NoteLog) if (n.StartsWith($"Level {level}:")) noted = true;
        Check(noted, $"level {level}: a margin note says what arrived");
        _gifts++;
    }

    private async Task PaveByClick()
    {
        Main.Driver.SetPaused(true);
        int r = T.SuggestedPave();
        // Put the road in the middle of the view, clear of the interface.
        var road = T.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell), Main.Rig.Camera.Size, instant: true);
        await Frames(3);
        await Key(Godot.Key.P);
        Check(Main.Hud.PaveMode, "P switches Pave mode on");
        Check(Main.Hud.LitRoads > 0, "worn-in roads light up in Pave mode");
        var at = Main.Hud.RoadScreen(r);
        TestInput.Motion(at);
        await Frames(3);
        Check(Main.Hud.HoverRoad == r, $"hovering the road picks it (hover {Main.Hud.HoverRoad}, road {r})");
        double coin = T.Coin, price = T.PavePrice(r);
        await Click(at);
        Check(T.RoadInfo(r).Surface == Surface.Gravel, "clicking the road paved it with gravel");
        Check(Math.Abs(T.Coin - (coin - price)) < 1e-6, "paving cost its price");
        await Frames(3);
        Check(Main.Ground.GravelCells > 0, "the gravel is drawn");
        if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
        Check(!Main.Hud.PaveMode, "Esc leaves Pave mode");
        Main.Rig.UserMoved = false;
        Main.Driver.SetPaused(false);
        _bought++;
        Notes.Add($"paved={r}");
    }

    private async Task BellByKey()
    {
        int rings = T.BellRings;
        await Key(Godot.Key.Space);
        Check(T.BellRings == rings + 1, "Space rings the bell");
        await Frames(10);
        Check(Main.Buildings.BellSwing > 0, "the bell swings");
        Check(Main.Hud.BellButton.Disabled, "the bell rests after ringing");
    }

    private async Task BellByClick()
    {
        int rings = T.BellRings;
        await Click(TestInput.Centre(Main.Hud.BellButton));
        Check(T.BellRings == rings + 1, "clicking the bell rings it");
    }
}
