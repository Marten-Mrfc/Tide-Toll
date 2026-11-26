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
        _bot.NoPave = true; // until u36, so a worn road is there to point at
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
        _bot.NoPave = false;

    }

    private static bool IsSkipper(Town t) => t.Request.Active && (t.Request.WhoName ?? "").Contains("skipper", StringComparison.OrdinalIgnoreCase);

    private async Task SecondAgeLate()
    {
        // u32: the middle: ferry, tide dial, a skipper's request.
        bool skipper = await Early(t => t.Ferry.Built && t.Request.Active && IsSkipper(t), 60);
        if (!skipper) await Early(t => (t.Ferry.Built || t.Level >= 22) && t.Request.Active, 60);
        if (!T.HasSeal)
        {
            await ClosePage(); await ClearChoice();
            await FollowView();
            await Settle(500);
            await Calm(8000);
            await Shot("u32", "Second age middle: ferry, tide dial, request", $"bot until the ferry was built and a request ran (skipper's={IsSkipper(T)}: '{T.Request.WhoName}'), level {T.Level}; tide dial={Main.Hud.TideVisible}");
        }
        else NotMade("u32", "Second age middle", "the seal came first");

        // u35: the busiest moment of the second age.
        await Busiest("u35", "Busiest moment (second age)", T.Level + 1, T.Level + 4);

        // u34: late, drawbridge and toll, at rest.
        if (await Early(t => t.CrossingExists(RiverLinkKind.Drawbridge) && t.Tolls > 0, 240) || await Early(t => t.CrossingExists(RiverLinkKind.Drawbridge), 60))
        {
            await ClosePage(); await ClearChoice();
            await FollowView();
            await Settle(500);
            await Calm();
            await Shot("u34", "Second age late at rest (drawbridge, toll)", $"bot until the drawbridge stood and a toll was paid (tolls={T.Tolls}), level {T.Level}");
        }
        else NotMade("u34", "Second age late at rest", "no drawbridge before the seal");

        // u37: the seal drive card: the second age has no drive of its own in this build (only the charter drive);
        // the nearest: the last level before the seal, at rest, with whatever card shows.
        if (await Early(t => t.Drive.Active, 60))
        {
            await ClosePage(); await FollowView(); await Settle(400); await Calm(8000);
            await Shot("u37", "Seal drive card", $"a drive ran in the second age ({T.DriveText}); card={Main.Hud.DriveCardVisible}");
        }
        else
        {
            int last = T.Level;
            await Early(t => t.Level >= t.MaxLevel - 1, 240);
            await ClosePage(); await ClearChoice();
            await FollowView(); await Settle(400); await Calm(8000);
            await Shot("u37", "Seal drive card (nearest: last level before the seal)",
                $"NOT the drive: the simulation runs no drive in the second age (Drive is the charter drive only). Nearest: level {T.Level} (from {last}), just before the seal, at rest; drive card={Main.Hud.DriveCardVisible}");
        }

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
