using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>The audit's zoom stages, pause cover, New town question and settings pages (u17..u25), and the zoom stages
/// of the second age (u40, u41).</summary>
public partial class UiAudit
{
    /// <summary>Page stage (the slim strip) and book stage (the summary page).</summary>
    private async Task Stages(string pageId, string bookId, string age)
    {
        await ClosePage();
        await FollowView();
        await Settle(400);
        var pivot = Main.Rig.PivotLocal;
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(pivot, Main.Rig.PlaySize, instant: true);
        await WaitFor(() => Math.Abs(Main.Rig.Camera.Size - Main.Rig.TargetSize) < 0.5f && Main.Rig.InPlayView && !Main.Cover.Moving
                            && Main.Hud.FullAlpha <= 0f, 8000);
        await Wait(600);
        await Shot(pageId, $"Page stage ({age} age)", $"camera to Rig.PlaySize (the whole page); slim strip={Main.Hud.SlimVisible}, stage={Main.Stage}, size={Main.Rig.Camera.Size:0} (page {Main.Rig.PlaySize:0}, book {Main.Rig.MaxSize:0})");
        Main.Rig.LookAt(pivot, Main.Rig.MaxSize, instant: true);
        await Wait(1500);
        await Shot(bookId, $"Book stage ({age} age)", $"camera to Rig.MaxSize (the open book, summary on the left page); stage={Main.Stage}");
        await FollowView();
        await Wait(800);
    }

    /// <summary>The settings spread at rest now: the pause menu in Settings, its motion not running, the arriving leaf
    /// fully turned (Arrival 1) and no tab's page turning.</summary>
    private bool SpreadRestNow(SettingsPages pages) =>
        Main.Menu.Now == PauseMenu.State.Settings && !Main.Menu.Motion.Running && pages.Arrival >= 1f && !pages.Turning && pages.TurnProgress >= 1f;

    /// <summary>Waits until <see cref="SpreadRestNow"/> holds for three frames running.</summary>
    private async Task<bool> SpreadAtRest(SettingsPages pages, int maxMs)
    {
        int held = 0;
        return await WaitFor(() => (held = SpreadRestNow(pages) ? held + 1 : 0) >= 3, maxMs);
    }

    private async Task MenusAndBook(string age)
    {
        await Stages("u17", "u18", age);

        // u19: the pause cover.
        Main.Menu.Pause();
        await WaitFor(() => Main.Menu.CoverShown, 20000);
        await Wait(400);
        await Shot("u19", "Pause cover", "Main.Menu.Pause() (as Esc), waited for the cover menu");

        // u20: New town question (No afterwards: the town is kept).
        Main.Menu.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(4);
        await WaitFor(() => !Main.Menu.Motion.Running, 5000);
        await Wait(400);
        if (Main.Menu.QuestionShown) await Shot("u20", "New town question", "cover's New town by focus and Enter; answered No (Esc) after");
        else NotMade("u20", "New town question", "the question did not show");
        await Key(Godot.Key.Escape);
        await Frames(4);
        await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 20000);

        // u21..u25: settings pages from the cover. (Round L3: in an --only run the u20 question's Esc may have resumed the
        // game: pause again first.)
        if (Main.Menu.Now == PauseMenu.State.Playing) { Main.Menu.Pause(); await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 60000); }
        var store = Main.Settings.Store;
        Key pave0 = store.KeyFor("tt_pave", 0), bell0 = store.KeyFor("tt_bell", 0);
        string[] ids = { "u21", "u22", "u23", "u24" };
        for (int i = 0; i < SettingsPages.Sections.Length && i < ids.Length; i++)
        {
            string sec = SettingsPages.Sections[i];
            // L2 (the L1 fixer's left-over): the u20 question's Esc may have resumed the game; pause first, then wait for
            // the cover. Under load the cover's motion takes many frames (BookMotion advances at most MaxStep a frame).
            if (Main.Menu.Now == PauseMenu.State.Playing && !Main.Menu.Motion.Running) { Main.Menu.Pause(); await Frames(4); }
            await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 60000);
            string opened = "cover's Settings by Enter";
            for (int tries = 0; tries < 3 && Main.Menu.Pages == null; tries++)
            {
                Main.Menu.SettingsButton.GrabFocus();
                await Key(Godot.Key.Enter);
                await WaitFor(() => Main.Menu.Pages != null, 15000);
            }
            if (Main.Menu.Pages == null && Main.Menu.Now == PauseMenu.State.Cover)
            {
                // Enter did not reach the button (its focus was taken while the lettering came in): the button's own action.
                Main.Menu.OpenSettings();
                opened = "PauseMenu.OpenSettings() (Enter on the cover's Settings had not opened them)";
                await WaitFor(() => Main.Menu.Pages != null, 15000);
            }
            if (Main.Menu.Pages == null) { NotMade(ids[i], $"Settings: {sec}", $"the settings did not open from the cover (three tries and OpenSettings; state {Main.Menu.Now})"); continue; }
            var pages = Main.Menu.Pages!;
            // The spread must be at rest before the tab is chosen and before the picture: the arriving leaf has turned
            // (state Settings, its motion ended, Arrival 1), then the tab's own page turn has ended.
            bool arrived = await SpreadAtRest(pages, 40000);
            pages.ShowSection(sec);
            await Frames(4);
            bool rest = arrived && await SpreadAtRest(pages, 40000);
            await Wait(500);
            await Frames(3);
            rest = rest && SpreadRestNow(pages);
            if (rest) await Shot(ids[i], $"Settings: {sec}", $"{opened}; waited for the spread at rest (state {Main.Menu.Now}, motion running={Main.Menu.Motion.Running}, arrival={pages.Arrival:0.##}), then section {sec} by SettingsPages.ShowSection and waited for its page turn to end (turning={pages.Turning}, turn={pages.TurnProgress:0.##}); section shown={pages.SectionShown(i):0.##}");
            else NotMade(ids[i], $"Settings: {sec}", $"the spread was not at rest within 40 s (state {Main.Menu.Now}, motion running={Main.Menu.Motion.Running}, arrival={pages.Arrival:0.##}, turning={pages.Turning})");
            if (sec == "Keys")
            {
                await Click(TestInput.Centre(pages.Controls["keys.tt_pave.0"]));
                await Key(Godot.Key.Space);
                await Frames(4);
                await Wait(500);
                await Shot("u25", "Key rebound with a clash", $"Keys page: Pave's key clicked, Space pressed (the bell's key); slip={pages.Slip}; cancelled with Esc after");
                await Key(Godot.Key.Escape);
                await Frames(3);
                Check(store.KeyFor("tt_pave", 0) == pave0 && store.KeyFor("tt_bell", 0) == bell0, "the cancelled clash changed a key");
            }
            await Key(Godot.Key.Escape);
            await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 60000);
        }

        // Back to the game.
        Main.Menu.ResumeButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(await WaitFor(() => !Main.Menu.IsPaused, 30000), "Resume opened the book");
        await Frames(3);
        await FollowView();
    }
}
