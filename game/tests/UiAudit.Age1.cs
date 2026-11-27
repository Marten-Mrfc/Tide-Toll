using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The audit's first age: u01..u16.</summary>
public partial class UiAudit
{
    private async Task FirstAge()
    {
        // u01: the first minute, the normal follow view.
        await FollowView();
        await Settle(300);
        var vm = Main.Hud.Mark(ShopItemKind.Villager);
        bool any = false; T.ShopItems(_items); foreach (var it in _items) any |= it.Affordable;
        await Shot("u01", "First minute: one cottage, nothing affordable",
            $"new town (seed 1) as it opens, follow view, t={T.Time:0}s coin={T.Coin:0}; anything affordable={any}");
        await TipShot("u42", "Tooltip: a shop row not yet affordable", Main.Hud.Mark(ShopItemKind.Villager), "the Villager row at the start");
        await TipShot("u44", "Tooltip: a navigation button", Main.Hud.AchievementsMark, "the achievements button");

        // u02: the first purchase affordable (the town runs by itself until the villager bookmark is enabled).
        await Idle(t => Affordable(t, ShopItemKind.Villager), 5);
        await Frames(3);
        await FollowView();
        await Settle(500);
        await Shot("u02", "First purchase affordable", "town left to run alone until the villager bookmark became enabled");

        // Buy it by a real click.
        vm = Main.Hud.Mark(ShopItemKind.Villager);
        if (vm != null && !vm.Disabled) await Click(TestInput.Centre(vm));

        // u04: level 2 reached, the banner showing.
        await Fast(t => t.Level >= 2, 20);
        await FollowView();
        await Settle(300);
        if (Main.Hud.BannerText.Length > 0)
            await Shot("u04", "Level banner showing", $"bot played to level 2; picture taken while the banner shows ({Main.Hud.BannerText})");
        else NotMade("u04", "Level banner showing", "the banner had gone before the drawing caught up");

        // u03: early shop with locked and affordable lines (level 2 or 3).
        await Calm();
        await Idle(AnyAffordableAndLocked, 3);
        var kinds = ShopKinds();
        await FollowView();
        await Settle(400);
        await Shot("u03", "Early shop: locked and affordable lines",
            $"level {T.Level} at rest, town left to run until a line is affordable; buy={kinds.buy} coin={kinds.coin} locked={kinds.locked} finished={kinds.done}");

        // u09: the bell ringing (Space), the bot does not ring it before.
        await Idle(t => t.BellReady, 3);
        if (T.BellReady)
        {
            await Key(Godot.Key.Space);
            await Wait(800);
            await Shot("u09", "The bell ringing", $"bell rung by its key (Space) at level {T.Level}; ringing={T.BellRinging}");
            await TipShot("u43", "Tooltip: the bell", Main.Hud.BellButton, "the bell while it rings");
        }
        else NotMade("u09", "The bell ringing", "the bell was not ready within three minutes");
        _bot.NoBell = false;

        // u05: the hint line and a request card together.
        bool both = await Fast(t => t.Request.Active && t.Request.Delivered > 0, 20);
        await FollowView();
        await Settle(400);
        await WaitFor(() => Main.Hud.HintText.Length > 0 && Main.Hud.RequestCardVisible, 15000);
        await Shot("u05", "Hint line and request card",
            $"bot played until a request ran with deliveries (level {T.Level}); hint='{Main.Hud.HintText}' request card={Main.Hud.RequestCardVisible}" + (both ? "" : " (no request within 20 min)"));

        // u06: the choice of two boons open.
        if (await Fast(t => t.ChoiceOpen, 30))
        {
            await FollowView();
            await Settle(500);
            await WaitFor(() => Main.Hud.ChoiceCardVisible, 5000);
            await Shot("u06", "Choice of two boons open", $"bot played until the simulation offered a choice (level {T.Level}); left open for the picture, then side A taken");
            await TipShot("u45", "Tooltip: a boon of the choice", Main.Hud.BoonButton(0), "side A of the choice");
            await ClearChoice();
        }
        else NotMade("u06", "Choice of two boons open", "no choice offered by level " + T.Level);

        await ExtendAndPave();

        // u12: a building offer and staked plots with their labels.
        int offer = -1;
        if (await Fast(t => { offer = OpenOffer(t); return offer >= 0; }, 30))
        {
            await ClearChoice();
            var pl = T.Plan.Plots[T.Plan.Offers[offer].Plot];
            await Look(pl.X + pl.W / 2.0, pl.Y + pl.H / 2.0, 70f);
            await Settle(600);
            await Calm(8000);
            await Shot("u12", "Building offer and staked plots", $"bot played until an offer was on sale (offer {offer}, level {T.Level}); camera on its plot at size 70 (labels of staked plots)");
        }
        else NotMade("u12", "Building offer and staked plots", "no offer on sale");

        // u10: the middle of the age at rest.
        await Fast(t => t.Level >= 8, 60);
        await ClearChoice();
        await FollowView();
        await Settle(600);
        await Calm();
        await Shot("u10", "Middle of the age at rest", $"bot to level {T.Level}, banners and notes gone, follow view");

        // u11: the busiest moment: a level-up with a request running and coin rising.
        await Busiest("u11", "Busiest moment of the middle game", 9, 13);
    }

    private readonly System.Collections.Generic.List<ShopItem> _items = new();

    /// <summary>From the simulation (the bookmarks update only with frames).</summary>
    private bool Affordable(Town t, ShopItemKind kind)
    {
        t.ShopItems(_items);
        foreach (var it in _items) if (it.Kind == kind && it.Affordable) return true;
        return false;
    }

    private bool AnyAffordableAndLocked(Town t)
    {
        t.ShopItems(_items);
        bool buy = false, locked = false;
        foreach (var it in _items) { if (it.Affordable) buy = true; else if (TownHud.IsLocked(it)) locked = true; }
        return buy && locked;
    }

    private static int OpenOffer(Town t)
    {
        for (int o = 0; o < t.Plan.Offers.Length; o++) if (t.OfferAvailable(o) && !t.OfferBought(o)) return o;
        return -1;
    }

    /// <summary>Plays level by level from minLevel until a level-up comes with a request running; the picture is taken
    /// 1.2 s after the level-up (banner, notes, coin numbers, request card, whatever the game shows).</summary>
    private async Task Busiest(string id, string title, int minLevel, int maxLevel, Func<Town, bool>? running = null)
    {
        running ??= t => t.Request.Active;
        int lv = Math.Max(T.Level + 1, minLevel);
        string how = "";
        for (; lv <= maxLevel; lv++)
        {
            int want = lv;
            await Fast(t => t.Level >= want || t.HasSeal, 60);
            if (T.HasSeal) break;
            if (running(T) || lv == maxLevel)
            {
                await FollowView();
                await Wait(1200);
                how = $"bot played level by level from {minLevel}; at the level-up to {T.Level} a request was running={running(T)} (town's={T.Request.Active}); taken 1.2 s after the level-up at normal speed, nothing cleared: banner='{Main.Hud.BannerText}', notes={Main.Hud.NotesShown}, request card={Main.Hud.RequestCardVisible}, choice={Main.Hud.ChoiceCardVisible}, drive={Main.Hud.DriveCardVisible}";
                break;
            }
        }
        if (T.HasSeal) NotMade(id, title, $"the seal came before a level-up in {minLevel}..{maxLevel} with a request");
        else await Shot(id, title, how);
        await ClearChoice();
    }

    private async Task ExtendAndPave()
    {
        // u07: Extend mode with the pointer on a cottage.
        int cot = -1;
        bool ok = await Fast(t => t.Level >= 5, 30)
                  && (await Idle(t => (cot = Extendable(t)) >= 0, 5) || await Fast(t => (cot = Extendable(t)) >= 0, 30));
        cot = Extendable(T);
        await ClearChoice();
        if (ok && cot >= 0)
        {
            await LookPlot(cot, 34f);
            await Settle(600);
            await Calm(10000);
            var em = Main.Hud.Mark(ShopItemKind.Extend);
            if (em != null) await Click(TestInput.Centre(em));
            TestInput.Motion(Main.Hud.PlotScreen(cot));
            await Frames(4);
            await Wait(600);
            if (Main.Hud.ExtendMode)
                await Shot("u07", "Extend mode, pointer on a cottage", $"Extend bookmark clicked, pointer moved onto cottage plot {cot} (level {T.Level}); tip='{Main.Hud.HoverTipText}'");
            else NotMade("u07", "Extend mode, pointer on a cottage", "the Extend bookmark did not start Extend mode");
            if (Main.Hud.ExtendMode) await Key(Godot.Key.Escape);
        }
        else NotMade("u07", "Extend mode, pointer on a cottage", "no cottage could be extended");

        // u08: Pave mode with the pointer on a road.
        int road = -1;
        if (await Idle(t => (road = Pavable(t)) >= 0, 5) || await Fast(t => (road = Pavable(t)) >= 0, 30))
        {
            road = Pavable(T);
            await ClearChoice();
            await LookRoad(road, 34f);
            await Settle(600);
            await Calm(10000);
            var pm = Main.Hud.Mark(ShopItemKind.Pave);
            if (pm != null) await Click(TestInput.Centre(pm));
            TestInput.Motion(Main.Hud.RoadScreen(road));
            await Frames(4);
            await Wait(600);
            if (Main.Hud.PaveMode)
                await Shot("u08", "Pave mode, pointer on a road", $"Pave bookmark clicked, pointer moved onto road {road} (level {T.Level}); tip='{Main.Hud.HoverTipText}'");
            else NotMade("u08", "Pave mode, pointer on a road", "the Pave bookmark did not start Pave mode");
            if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
        }
        else NotMade("u08", "Pave mode, pointer on a road", "no road could be paved");
        TestInput.Motion(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        _bot.NoPave = false;
    }

    private static int Extendable(Town t)
    {
        for (int p = 0; p < t.Plan.Plots.Length; p++)
            if (t.Plan.Plots[p].Kind == PlotKind.Cottage && t.PlotGiven(p) && t.CanExtendNow(p)) return p;
        return -1;
    }

    private static int Pavable(Town t)
    {
        for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r) && t.CanPaveNow(r)) return r;
        return -1;
    }

    private async Task FirstAgeLate()
    {
        // u13: late in the age at rest with the full shop.
        await Fast(t => t.Level >= 15, 120);
        await ClearChoice();
        await FollowView();
        await Settle(600);
        await Calm();
        await Shot("u13", "Late in the age at rest, full shop", $"bot to level {T.Level}, banners and notes gone; drive card={Main.Hud.DriveCardVisible}");

        // u14: the charter drive card.
        if (await Fast(t => t.Drive.Active && t.Drive.Delivered > 0, 120))
        {
            await ClearChoice();
            await FollowView();
            await Settle(500);
            await Calm(8000);
            await Shot("u14", "Charter drive card", $"bot until the drive ran with deliveries ({T.DriveText}); card shown={Main.Hud.DriveCardVisible}");
        }
        else NotMade("u14", "Charter drive card", "no charter drive by level " + T.Level);

        // u15: the achievements page (Tab).
        await Key(Godot.Key.Tab);
        await WaitFor(() => Main.Hud.AchievementsOpen, 3000);
        await Wait(900);
        await Shot("u15", "Achievements page open", $"Tab at level {T.Level} (first age)");
        await ClosePage();

        // u16: the charter moment and the first age's page.
        if (await Fast(t => t.HasCharter, 600))
        {
            await FollowView();
            await Settle(300);
            bool page = await WaitFor(() => Main.Hud.AgePageOpen, 8000);
            await Wait(700);
            await Shot("u16", "Charter moment and the age page", $"bot until the charter; taken {(page ? "with the age page open (opened by itself)" : "without the age page (it did not open within 8 s)")}; banner='{Main.Hud.BannerText}'");
            await ClosePage();
        }
        else NotMade("u16", "Charter moment and the age page", "the bot did not reach the charter");
    }
}
