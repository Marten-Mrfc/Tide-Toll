using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-town: the town through real input (mouse and key events parsed by Godot, reaching the interface first).
/// Buys the first upgrades by clicking their bookmarks and by key, then plays at high speed by clicking until level 6
/// and at least one of every kind of shop item the rules have by then (Villager, Pace, Load, Tools, a value track,
/// the bell track, a building offer, Extend by clicking a cottage, Pave by clicking a road), checking that every
/// purchase changed the town, that each level's gift arrived and was drawn (plots, roads, the level banner), that a
/// road wore in visibly, that the bell works by key and by click, that the request card matches the simulation's
/// request and the hint line its hint (with the marker on its road or plot). Villagers never jump or slide sideways
/// at normal speed. Then (TownTest.Pave.cs) the end of the age by the simulation's bot and 30 Pave clicks, tilted and
/// flat, each landing on exactly the road the price tip named.
/// </summary>
public partial class TownTest : TownTestBase
{
    protected override string Marker => "TOWN PASS";
    private int _bought, _gifts;
    private readonly Dictionary<ShopItemKind, int> _kinds = new();
    private int _requestChecks, _hintChecks, _markerChecks;

    protected override async Task Run()
    {
        // A new road is a faint trail once drawn in.
        int r0 = FirstOpenRoad();
        await Until(() => Main.Ground.RoadReveal(r0) >= 1f, 600, "the first road drawn in");
        await Frames(30);
        float faint = Main.Ground.RoadShown(r0);
        Check(faint > 0.15f && faint < TownGround.TrailWear + 0.2f, $"a new road is a faint trail (drawn wear {faint:0.00})");
        // Round 6 (W2): no ford is drawn across the river in the first age.
        Check(!Main.Ground.FordShown, "no ford drawn across the river in the first age");
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

        // Every bookmark has its own key while it is shown.
        CheckKeysUnique();

        // Play fast with real clicks until level 6 and every kind bought, checking gifts, wear, the bell, requests and hints.
        Main.Driver.SetSpeed(32);
        bool wornSeen = false, bellKey = false, bellClick = false;
        int level = T.Level;
        var need = new[] { ShopItemKind.Villager, ShopItemKind.Pace, ShopItemKind.Load, ShopItemKind.Tools, ShopItemKind.Value,
            ShopItemKind.BellTrack, ShopItemKind.Building, ShopItemKind.Extend, ShopItemKind.Pave };
        for (int f = 0; f < 60 * 900 && (T.Level < 6 || !wornSeen || !bellKey || !bellClick || !AllBought(need)); f++)
        {
            await Frames(1);
            if (T.Level > level)
            {
                for (int l = level + 1; l <= T.Level; l++) await CheckGift(l);
                level = T.Level;
                CheckKeysUnique();
            }
            if (!wornSeen && T.RoadInfo(r0).WornIn)
            {
                await Frames(90);
                float firm = Main.Ground.RoadShown(r0);
                Check(firm > faint + 0.3f, $"a worn-in road is drawn firmer ({faint:0.00} to {firm:0.00})");
                Notes.Add($"worn={firm:0.00}");
                wornSeen = true;
            }
            if (T.ChoiceOpen && !_boonByClick) { await BoonByClick(); continue; }
            if (f % 20 == 0) { CheckRequestCard(); await CheckHint(); }
            if (Got(ShopItemKind.Pave) == 0 && PavableNow()) { await PaveByClick(); continue; }
            if (Got(ShopItemKind.Extend) == 0 && Main.Hud.Mark(ShopItemKind.Extend) is { Disabled: false }) { await ExtendByClick(); continue; }
            if (!bellKey && T.BellReady) { await BellByKey(); bellKey = true; continue; }
            if (bellKey && !bellClick && T.BellReady) { await BellByClick(); bellClick = true; continue; }
            if (f % 3 == 0) await BuyNext();
        }
        Check(T.Level >= 6, $"reached level 6 (level {T.Level})");
        Check(wornSeen && bellKey && bellClick, $"worn={wornSeen} bellKey={bellKey} bellClick={bellClick}");
        foreach (var k in need) Check(Got(k) >= 1, $"bought at least one {k} through the interface (bought {Kinds()})");
        Check(_requestChecks > 0, "a request was asked and its card checked");
        Check(_hintChecks > 0, "a hint was given and its line checked");
        Notes.Add($"level={T.Level} bought={_bought} {Kinds()} gifts={_gifts} requests={_requestChecks} hints={_hintChecks} markers={_markerChecks} time={T.Time:0}s");

        await RulesV3();
        await PaveAtTheEndOfTheAge();
    }

    private int Got(ShopItemKind k) => _kinds.TryGetValue(k, out int n) ? n : 0;
    private bool AllBought(ShopItemKind[] kinds) { foreach (var k in kinds) if (Got(k) == 0) return false; return true; }
    private string Kinds()
    {
        var parts = new List<string>();
        foreach (var kv in _kinds) parts.Add($"{kv.Key}:{kv.Value}");
        return string.Join(",", parts);
    }

    private bool PavableNow()
    {
        for (int r = 0; r < T.RoadCount; r++) if (Main.Hud.Pavable(r)) return true;
        return false;
    }

    private void CheckKeysUnique()
    {
        var seen = new HashSet<string>();
        var items = Main.Hud.ShownItems;
        for (int i = 0; i < items.Count && i < Main.Hud.Marks.Count; i++)
        {
            string key = Main.Hud.RowKey(i);
            if (key.Length == 0) continue; // a row without a key keeps the empty column
            Check(seen.Add(key), $"two rows share the key [{key}]");
        }
    }

    private int FirstOpenRoad()
    {
        for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r)) return r;
        throw new Exception("no open road at the start");
    }

    private async Task WatchVillagers(int frames)
    {
        Main.Driver.SetSpeed(1);
        int sideways = 0, moving = 0, overlapped = 0;
        float maxStep = 0;
        for (int i = 0; i < frames; i++)
        {
            await Frames(1);
            maxStep = Math.Max(maxStep, Main.Villagers.MaxStepCells);
            if (Main.Villagers.MaxStepCells > 0.002f) moving++;
            if (Main.Villagers.MaxSideways > 0.6f) sideways++;
            if (Main.Villagers.MinStandingGap < 0.3f * Main.Villagers.WalkerScale * TownVillagers.Personal) overlapped++;
        }
        // Round 6 (W3): people who stand (queued or working) are never drawn inside each other.
        Check(overlapped <= frames / 5, $"standing villagers drawn apart ({overlapped} of {frames} frames with two closer than {0.3f * Main.Villagers.WalkerScale:0.00} m)");
        // Round 6 (W6): each Load step is drawn bigger, with a second bundle from the second step.
        Check(TownVillagers.LoadScale(1) > TownVillagers.LoadScale(0) && TownVillagers.LoadScale(3) >= 1.3f * TownVillagers.LoadScale(1) && TownVillagers.BundlesAt(2) == 2, "each Load step shows on the villager");
        Check(moving > frames / 4, $"villagers walk ({moving} frames moving)");
        Check(maxStep < 0.5f, $"no villager jumps at 1x (largest step {maxStep:0.00} cells)");
        Check(sideways <= moving / 10, $"villagers face the way they walk ({sideways} of {moving} frames turned over 0.6 rad)");
        Notes.Add($"maxstep={maxStep:0.000}");
    }

    private async Task BuyByClick(ShopItemKind kind)
    {
        await Until(() => Main.Hud.Mark(kind) is { Disabled: false }, 60 * 120, kind + " affordable");
        await ClickMark(Main.Hud.ShownItems[IndexOf(kind)]);
    }

    private int IndexOf(ShopItemKind kind)
    {
        var items = Main.Hud.ShownItems;
        for (int i = 0; i < items.Count; i++) if (items[i].Kind == kind) return i;
        return -1;
    }

    /// <summary>Clicks a bookmark and checks the town changed the way that purchase should change it.</summary>
    private async Task ClickMark(ShopItem it)
    {
        int i = -1;
        var items = Main.Hud.ShownItems;
        for (int j = 0; j < items.Count; j++) if (items[j].Kind == it.Kind && items[j].Index == it.Index) i = j;
        if (i < 0) return;
        // Night 3: a row that moved in the last 0.3 s takes no click (layout sheet section 3): aim when it is still.
        await Until(() => Main.Hud.RowCalm(i), 120, "the row comes to rest");
        var b = Main.Hud.Marks[i];
        long purchases = T.Purchases;
        int villagers = T.VillagerCount, pace = T.PaceLevel, load = T.LoadLevel, built = Main.Buildings.BuiltCount;
        int tools = T.ToolsLevel, bellTrack = T.BellTrackLevel, value = it.Kind == ShopItemKind.Value ? T.ValueLevel((Good)it.Index) : 0;
        await Click(TestInput.Centre(b));
        if (T.Purchases == purchases) return; // the price moved past the purse between frames: not a failure of the click
        _bought++;
        _kinds[it.Kind] = Got(it.Kind) + 1;
        switch (it.Kind)
        {
            case ShopItemKind.Villager:
                Check(T.VillagerCount == villagers + 1, "a villager purchase adds a villager");
                await Until(() => Main.Villagers.Drawn == T.VillagerCount, 30, "the new villager drawn");
                break;
            case ShopItemKind.Pace: Check(T.PaceLevel == pace + 1, "a pace purchase raises pace"); break;
            case ShopItemKind.Load: Check(T.LoadLevel == load + 1, "a load purchase raises load"); break;
            case ShopItemKind.Tools: Check(T.ToolsLevel == tools + 1, "a tools purchase raises tools"); break;
            case ShopItemKind.BellTrack: Check(T.BellTrackLevel == bellTrack + 1, "a bell purchase raises the bell track"); break;
            case ShopItemKind.Value: Check(T.ValueLevel((Good)it.Index) == value + 1, $"a {(Good)it.Index} price purchase raises its value"); break;
            case ShopItemKind.Building:
                await Until(() => Main.Buildings.BuiltCount > built, 60, "the bought building drawn");
                break;
        }
    }

    /// <summary>A sensible player: a kind not bought yet, else a villager or a building, else the cheapest.</summary>
    private async Task BuyNext()
    {
        int best = -1, bestRank = 9;
        var items = Main.Hud.ShownItems;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.Kind is ShopItemKind.Pave or ShopItemKind.Extend || !it.Affordable) continue;
            int rank = Got(it.Kind) == 0 ? 0 : it.Kind is ShopItemKind.Villager or ShopItemKind.Building ? 1 : 2;
            if (best < 0 || rank < bestRank || (rank == bestRank && it.Price < items[best].Price)) { best = i; bestRank = rank; }
        }
        if (best < 0) return;
        await ClickMark(items[best]);
    }

    private async Task CheckGift(int level)
    {
        var def = T.Plan.Level(level);
        foreach (int p in def.Plots)
        {
            var plot = T.Plan.Plots[p];
            if (plot.Offer >= 0)
            {
                await Until(() => Main.Buildings.PlotStaked(p), 30, $"level {level}: offer plot {p} staked");
                // Round 4 (A19): the stakes are pale.
                var stakes = Main.Buildings.FindChild($"Stakes{p}", true, false)?.GetNodeOrNull<MeshInstance3D>("Model");
                Check(stakes?.MaterialOverride is StandardMaterial3D sm && sm.AlbedoColor.Luminance > 0.6f, $"level {level}: the stakes on plot {p} are not pale");
            }
            else await Until(() => Main.Buildings.PlotDrawn(p), 30, $"level {level}: plot {p} drawn");
        }
        foreach (int r in def.Roads)
        {
            await Until(() => Main.Ground.RoadReveal(r) >= 1f, 1200, $"level {level}: road {r} drew itself in");
            await Until(() => Main.Ground.RoadShown(r) > 0.15f, 120, $"level {level}: road {r} drawn");
        }
        bool banner = false;
        foreach (var n in Main.Hud.BannerLog) if (n.StartsWith($"Level {level}:")) banner = true;
        // Night 3, L2: a level that opens a choice says its gift as a note (the choice, headed with the level, is the
        // large message: a decision is never delayed by a banner).
        bool choiceLevel = T.ChoiceOpen && T.Choice.Level == level;
        if (choiceLevel) foreach (var n in Main.Hud.NoteLog) if (n.StartsWith($"Level {level}:")) banner = true;
        Check(banner, $"level {level}: a {(choiceLevel ? "note" : "banner")} says what arrived");
        if (choiceLevel) Check(Main.Hud.ChoiceCardText.StartsWith($"Level {level}:"), $"level {level}: the choice's heading names the level ('{Main.Hud.ChoiceCardText}')");
        // Round 4 (A18): while the banner shows, it never covers the gift it announces.
        for (int f = 0; f < 30 && level == T.Level; f++)
        {
            if (Main.Hud.BannerText.StartsWith($"Level {level}:") && Main.Hud.BannerGiftScreen is { } g && Main.Hud.FullAlpha >= 1f)
            {
                // Night 3: the banner has one fixed place, the Message zone (layout sheet section 2; the gift may lie
                // under it: "a zone never moves"). Was: the banner never covers its gift.
                var z = new HudZones(GetViewport().GetVisibleRect().Size, Main.Hud.UiScale);
                Check(Main.Hud.BannerRect.IsEqualApprox(z.Banner), $"level {level}: the banner {Main.Hud.BannerRect} is not in the Message zone {z.Banner} (gift at {g})");
            }
            await Frames(1);
        }
        _gifts++;
    }

    /// <summary>The request card shows the simulation's request: who, how much of what, delivered, time left, reward.</summary>
    private void CheckRequestCard()
    {
        var r = T.Request;
        if (!r.Active || Main.Hud.FullAlpha < 1f) return;
        Check(Main.Hud.RequestCardVisible, "a running request shows its card");
        string card = Main.Hud.RequestCardText;
        string good = TownHud.GoodName(r.Good);
        Check(card.Contains(r.Amount + " " + good), $"the card names the amount and good ({card})");
        Check(card.Contains(r.WhoName.ToLowerInvariant().Substring(4)), $"the card names who asks ({card})");
        Check(card.Contains($"Delivered {Math.Min(r.Delivered, r.Amount)} of {r.Amount}"), $"the card shows what is delivered ({card})");
        Check(card.Contains("Reward: " + TownHud.Money(r.RewardCoin)), $"the card shows the reward ({card})");
        float bar = Main.Hud.RequestCountBar, want = Math.Min(r.Delivered, r.Amount) / (float)Math.Max(1, r.Amount);
        Check(Math.Abs(bar - want) < 0.08f, $"the goal bar shows the count delivered ({bar:0.00} vs {want:0.00})");
        float left = Main.Hud.RequestTimeBar, wantLeft = (float)(r.SecondsLeft / r.Seconds);
        Check(Math.Abs(left - wantLeft) < 0.08f, $"the goal knows the time left ({left:0.00} vs {wantLeft:0.00})");
        _requestChecks++;
    }

    /// <summary>The hint line under the ledger says the simulation's hint, with a marker on its road or plot.</summary>
    private async Task CheckHint()
    {
        string h = T.Hint;
        if (h.Length == 0 || Main.Hud.PaveMode || Main.Hud.ExtendMode || Main.Hud.FullAlpha < 1f) return;
        int road = T.HintRoad, plot = T.HintPlot;
        Main.Driver.SetPaused(true);
        await Frames(2);
        string shown = Main.Hud.HintText;
        // A hint is held a few seconds, so the line may still show the one before.
        if (shown == h)
        {
            _hintChecks++;
            if (road >= 0 || plot >= 0)
            {
                // Night 3, L2: the ring shows only while the line shows the town's hint (a refusal, a mode's text or a
                // first-minute hint take the line first, message-system section 1).
                if (Main.Hud.LineKindNow == TownHud.LineKind.TownHint)
                {
                    Check(Main.Hud.HintMarkerVisible, $"the hint '{h}' has a marker");
                    Check(Main.Hud.HintMarkerRoad == road && Main.Hud.HintMarkerPlot == plot, $"the marker points where the hint says (road {road}, plot {plot})");
                    _markerChecks++;
                }
                else Check(!Main.Hud.HintMarkerVisible || Main.Hud.HintMarkerAlpha < 1f, $"the ring shows while the line says '{Main.Hud.LineText}' ({Main.Hud.LineKindNow})");
            }
        }
        else Check(shown.Length > 0, $"a hint is given ('{h}') but the line is empty");
        Main.Driver.SetPaused(false);
    }

    private async Task ExtendByClick()
    {
        Main.Driver.SetPaused(true);
        // Put the suggested cottage in the middle of the view (a still camera: the follow view eases).
        int sug = T.SuggestedExtend();
        var pl = T.Plan.Plots[sug];
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(new Vector3((pl.X + pl.W / 2f) * TownGround.Cell, 0, (pl.Y + pl.H / 2f) * TownGround.Cell), Main.Rig.Camera.Size, instant: true);
        await Frames(3);
        long purchases = T.Purchases;
        await Click(TestInput.Centre(Main.Hud.Mark(ShopItemKind.Extend)!));
        Check(Main.Hud.ExtendMode, "the Extend bookmark switches Extend mode on");
        Check(Main.Hud.LitCottages > 0, "cottages that can be extended light up");
        int p = T.SuggestedExtend();
        Check(p >= 0, "a cottage is suggested");
        await Frames(3);
        var at = Main.Hud.PlotScreen(p);
        TestInput.Motion(at);
        await Frames(3);
        Check(Main.Hud.HoverCottage == p, $"hovering the suggested cottage picks it (hover {Main.Hud.HoverCottage}, cottage {p}, mode {Main.Hud.ExtendMode}, ok {Main.Hud.Extendable(p)}, at {at}, direct {Main.Hud.ExtendTarget(at)}, gui {GetViewport().GuiGetHoveredControl()?.Name})");
        Check(Main.Hud.HoverTipText.Contains("Extend this cottage"), $"the tip names the price ({Main.Hud.HoverTipText})");
        // Round 6: the price tip sits beside the pointer, clear of the cottage, and no coin number shows in the mode.
        Check(!Main.Hud.HoverTipRect.Intersects(Main.Hud.HoverCottageRect), $"the price tip {Main.Hud.HoverTipRect} lies over the cottage {Main.Hud.HoverCottageRect}");
        // Night 3, L2: the numbers fade out over 0.15 s when the mode starts (was: gone in that frame).
        await Until(() => Main.Hud.FloatersVisible == 0, 30, "no coin number shows while Extend points at a cottage");
        int room = T.CottageRoom(p);
        await Click(at);
        Check(T.Extensions(p) == 1 && T.CottageRoom(p) == room + 1, "clicking the cottage extended it");
        Check(T.Purchases == purchases + 1, "extending is a purchase");
        Check(!Main.Hud.ExtendMode, "Extend mode ends after an extension");
        await Until(() => Main.Buildings.DrawnExtensions(p) == 1, 30, "the extended cottage drawn bigger");
        // Night 3 (replaces round 4's lean-to check): every size is its own model; the extended cottage is drawn with
        // the once-extended model of its variant, whose footprint is larger than the plain one's.
        string drawn = Main.Buildings.DrawnModel(p), plain = drawn.Length > 0 ? drawn[..^1] + "0" : "";
        var da = ModelLib.Mesh(drawn).GetAabb(); var pa = ModelLib.Has(plain) ? ModelLib.Mesh(plain).GetAabb() : da;
        Check(drawn.EndsWith("1") && da.Size.X * da.Size.Z > 1.2f * pa.Size.X * pa.Size.Z, $"the extended cottage is drawn with its own larger model ({drawn})");
        _kinds[ShopItemKind.Extend] = Got(ShopItemKind.Extend) + 1;
        _bought++;
        Main.Rig.UserMoved = false;
        Main.Driver.SetPaused(false);
        Notes.Add($"extended={p}");
    }

    private async Task PaveByClick()
    {
        Main.Driver.SetPaused(true);
        int r = T.SuggestedPave();
        if (!Main.Hud.Pavable(r)) for (r = 0; r < T.RoadCount && !Main.Hud.Pavable(r); r++) { }
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
        int tip = Main.Hud.HoverRoad;
        Check(tip >= 0 && Main.Hud.HoverTipText.Contains(TownHud.Money(T.PavePrice(tip))), $"hovering a road names it with its price (hover {tip}, '{Main.Hud.HoverTipText}')");
        double coin = T.Coin, price = T.PavePrice(tip);
        var s0 = T.RoadInfo(tip).Surface;
        await Click(at);
        Check(T.RoadInfo(tip).Surface != s0, "clicking paved the road the tip named");
        Check(Math.Abs(T.Coin - (coin - price)) < 1e-6, "paving cost its price");
        await Until(() => Main.Ground.GravelCells > 0, 120, "the gravel drawn");
        Check(!Main.Hud.PaveMode, "Pave mode ends after a pave");
        Main.Rig.UserMoved = false;
        Main.Driver.SetPaused(false);
        _kinds[ShopItemKind.Pave] = Got(ShopItemKind.Pave) + 1;
        _bought++;
        Notes.Add($"paved={tip}");
    }

    private async Task BellByKey()
    {
        int rings = T.BellRings;
        await Key(Godot.Key.Space);
        Check(T.BellRings == rings + 1, "Space rings the bell");
        await Frames(10);
        Check(Main.Buildings.BellSwing > 0, "the bell swings");
        Check(Main.Hud.BellButton.Disabled, "the bell rests after ringing");
        // Round 6: while it rings the card says the time left and the rest after.
        // Night 3: the bell shows the seconds left; its tooltip has the whole sentence (layout sheet section 3).
        if (T.BellRinging) Check(Main.Hud.BellTipText.Contains(" s left, then rests "), $"the ringing bell's tooltip says the time left and the rest: {Main.Hud.BellTipText}");
        await LayoutChecks();
    }

    /// <summary>Round 6: the left column does not overlap, the shop keeps above the achievements card, coin numbers are
    /// one per spot, and the map's labels keep clear of the cards and of each other.</summary>
    private async Task LayoutChecks()
    {
        await Frames(3);
        var h = Main.Hud;
        // Night 3: the request is the ledger's goal section (inside the ledger, in its own rectangle), and the shop ends
        // above navigation (layout sheet section 2).
        var z = new HudZones(GetViewport().GetVisibleRect().Size, h.UiScale);
        if (h.RequestRect.Size.Y > 0) Check(h.LedgerRect.Encloses(h.RequestRect) && h.RequestRect.IsEqualApprox(z.Goal), $"the request {h.RequestRect} is the ledger's goal section {z.Goal}");
        var ach = h.AchievementsMark;
        if (ach.Visible) Check(h.ShopRect.End.Y <= ach.GetGlobalRect().Position.Y + 1, $"the shop {h.ShopRect} runs under the achievements card {ach.GetGlobalRect()}");
        var kept = h.LabelsShownRects;
        for (int i = 0; i < kept.Count; i++)
        {
            Check(!kept[i].Intersects(h.ShopRect.Grow(-2)) && !kept[i].Intersects(h.LedgerRect.Grow(-2)), $"a map label {kept[i]} lies on a card");
            for (int j = i + 1; j < kept.Count; j++) Check(!kept[i].Intersects(kept[j]), $"two map labels overlap: {kept[i]} {kept[j]}");
        }
    }

    private async Task BellByClick()
    {
        int rings = T.BellRings;
        await Click(TestInput.Centre(Main.Hud.BellButton));
        Check(T.BellRings == rings + 1, "clicking the bell rings it");
    }
}
