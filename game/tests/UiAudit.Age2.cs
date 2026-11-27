using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The audit's second age: u30..u41.</summary>
public partial class UiAudit
{
    /// <summary>Fast, never past the seal.</summary>
    private async Task<bool> Early(Func<Town, bool> cond, double maxMinutes)
    {
        await Fast(t => t.HasSeal || cond(t), maxMinutes);
        return !T.HasSeal && cond(T);
    }

    private async Task SecondAge()
    {
        // Ring in the next age by its key (N); the river age's page opens after the page turn.
        await ClosePage();
        await FollowView();
        await Calm();
        await Key(Godot.Key.N);
        await WaitFor(() => Main.Hud.PageTurning || Main.Hud.BannerText != "", 5000);
        if (Main.Hud.PageTurning) await WaitFor(() => !Main.Hud.PageTurning, 15000);
        if (!await WaitFor(() => Main.Hud.AgePageOpen, 30000)) { await Key(Godot.Key.N); await WaitFor(() => Main.Hud.AgePageOpen, 10000); }
        await Wait(700);
        if (Main.Hud.AgePageOpen) await Shot("u31", "River age's page open", "rung in by N after the charter; the page opened after the page turn");
        else NotMade("u31", "River age's page open", "the page did not open");
        await ClosePage();

        // u30: its first minute (the landing given).
        _bot.PlayOn = true;
        await FollowView();
        await Settle(600);
        await Calm(10000);
        await Shot("u30", "Second age, first minute", $"just after the ring and the page closed, follow view; level {T.Level}, landing given={T.RiverPlan != null}");

        // u33: the shop with the river group.
        await Early(t => t.Level >= 18, 60);
        await ClosePage(); await ClearChoice();
        await FollowView();
        await Settle(500);
        await Calm();
        await Shot("u33", "Shop with the river group", $"bot to level {T.Level} (second age) at rest");

        // u36: Pave or Extend mode on.
        int road = -1;
        if (await Early(t => (road = Pavable(t)) >= 0, 30))
        {
            road = Pavable(T);
            await ClosePage(); await ClearChoice();
            await LookRoad(road, 34f);
            await Settle(500);
            await Calm(8000);
            var pm = Main.Hud.Mark(ShopItemKind.Pave);
            if (pm != null) await Click(TestInput.Centre(pm));
            TestInput.Motion(Main.Hud.RoadScreen(road));
            await Frames(4);
            await Wait(600);
            if (Main.Hud.PaveMode) await Shot("u36", "Pave mode (second age)", $"Pave bookmark clicked, pointer on road {road} (level {T.Level}); tip='{Main.Hud.HoverTipText}'");
            else NotMade("u36", "Pave mode (second age)", "Pave mode did not start");
            if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
            TestInput.Motion(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        }
        else NotMade("u36", "Pave mode (second age)", "no pavable road before the seal");
    }

    /// <summary>A skipper's request is open: a boat that asks for goods, not gone or leaving (what the request card
    /// shows while no town request runs; skippers' requests are not <c>Town.Request</c>).</summary>
    private static bool SkipperAsks(Town t)
    {
        if (!t.InRiverAge) return false;
        for (int i = 0; i < t.BoatCount; i++)
        {
            var b = t.Boat(i);
            if (b.Request && b.Phase is not (BoatPhase.Gone or BoatPhase.Leaving)) return true;
        }
        return false;
    }

    /// <summary>A request of either kind is running (the town's or a skipper's).</summary>
    private static bool AnyRequest(Town t) => t.Request.Active || SkipperAsks(t);

    private bool SkipperCardShown => Main.Hud.RequestCardText.Contains("skipper", StringComparison.OrdinalIgnoreCase);

    private async Task SecondAgeLate()
    {
        // u32: the middle: the ferry, the tide dial, a skipper's request on the card. Staged on the ferry standing and a
        // skipper asking (bounded at level 24, never past the seal); at rest; then the card must show the skipper.
        bool ferry = false, card = false;
        for (int tries = 0; tries < 4 && !card && !T.HasSeal; tries++)
        {
            ferry = await Early(t => (t.CrossingExists(RiverLinkKind.Ferry) && SkipperAsks(t)) || t.Level >= 24, 90);
            if (!ferry && !T.HasSeal) ferry = await Early(t => t.CrossingExists(RiverLinkKind.Ferry), 60);
            if (T.HasSeal || !T.CrossingExists(RiverLinkKind.Ferry)) break;
            await ClosePage(); await ClearChoice();
            await FollowView();
            await Settle(500);
            await Calm(8000);
            card = await WaitFor(() => SkipperCardShown, 6000);
            if (!card && T.Level >= 24) break;
        }
        if (!T.HasSeal && T.CrossingExists(RiverLinkKind.Ferry))
            await Shot("u32", "Second age middle: ferry, tide dial, skipper's request",
                $"bot until the ferry stood and a skipper asked (Town.Boat(i).Request), at rest, then waited for the card to name the skipper: card shows skipper={SkipperCardShown} ('{Main.Hud.RequestCardText}'), town request={T.Request.Active}, level {T.Level}; tide dial={Main.Hud.TideVisible}");
        else NotMade("u32", "Second age middle", T.HasSeal ? "the seal came first" : "no ferry");
        await BlockedRowShot();

        // u35: the busiest moment of the second age: a level-up in the middle of the age (20..24) with a request (the
        // town's or a skipper's) running, 1.2 s after, nothing cleared.
        await Busiest("u35", "Busiest moment (second age)", 20, 24, AnyRequest);

        // u34: late, drawbridge and toll, at rest. Staged on the drawbridge standing (never past the seal), then a short
        // bounded wait for the first toll.
        if (await Early(t => t.CrossingExists(RiverLinkKind.Drawbridge), 240))
        {
            await Early(t => t.Tolls > 0, 6);
            await ClosePage(); await ClearChoice();
            await FollowView();
            await Settle(500);
            await Calm();
            await Shot("u34", "Second age late at rest (drawbridge, toll)", $"bot until the drawbridge stood (CrossingExists(Drawbridge)), then up to 6 game minutes for a toll; at rest: drawbridge={T.CrossingExists(RiverLinkKind.Drawbridge)} tolls={T.Tolls}, level {T.Level}");
        }
        else NotMade("u34", "Second age late at rest", "no drawbridge before the seal");

        // (u37, the seal drive card, is not in the tour: the second age has no drive.)

        // u39 before the seal would show the river achievements in play; taken after the seal below.
        // u38: the borough seal moment and its page.
        if (await Fast(t => t.HasSeal, 600))
        {
            await ClosePage();
            await FollowView();
            await Settle(300);
            bool page = await WaitFor(() => Main.Hud.AgePageOpen, 8000);
            if (!page) { await Key(Godot.Key.N); page = await WaitFor(() => Main.Hud.AgePageOpen, 8000); }
            await Wait(700);
            await Shot("u38", "Borough seal moment and its page", $"bot until the seal; page open={page} (N, 'Read its page again', if it had not opened); banner='{Main.Hud.BannerText}'");
            await ClosePage();
        }
        else NotMade("u38", "Borough seal moment", "the bot did not reach the seal");

        await Key(Godot.Key.Tab);
        await WaitFor(() => Main.Hud.AchievementsOpen, 3000);
        await Wait(900);
        await Shot("u39", "Achievements page (second age)", $"Tab after the seal, level {T.Level}");
        await ClosePage();

        await Stages("u40", "u41", "second");
    }
}
