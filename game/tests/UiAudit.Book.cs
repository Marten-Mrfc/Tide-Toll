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
                            && Main.Hud.SlimVisible && Main.Hud.FullAlpha <= 0f, 8000);
        await Wait(600);
        await Shot(pageId, $"Page stage ({age} age)", $"camera to Rig.PlaySize (the whole page); slim strip={Main.Hud.SlimVisible}, stage={Main.Stage}");
        Main.Rig.LookAt(pivot, Main.Rig.MaxSize, instant: true);
        await Wait(1500);
        await Shot(bookId, $"Book stage ({age} age)", $"camera to Rig.MaxSize (the open book, summary on the left page); stage={Main.Stage}");
        await FollowView();
        await Wait(800);
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

        // u21..u25: settings pages from the cover.
        var store = Main.Settings.Store;
        Key pave0 = store.KeyFor("tt_pave", 0), bell0 = store.KeyFor("tt_bell", 0);
        string[] ids = { "u21", "u22", "u23", "u24" };
        for (int i = 0; i < SettingsPages.Sections.Length && i < ids.Length; i++)
        {
            string sec = SettingsPages.Sections[i];
            await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 20000);
            for (int tries = 0; tries < 3 && Main.Menu.Pages == null; tries++)
            {
                Main.Menu.SettingsButton.GrabFocus();
                await Key(Godot.Key.Enter);
                await WaitFor(() => Main.Menu.Pages != null, 10000);
            }
            if (Main.Menu.Pages == null) { NotMade(ids[i], $"Settings: {sec}", "the settings did not open from the cover (three tries)"); continue; }
            var pages = Main.Menu.Pages!;
            pages.ShowSection(sec);
            await Frames(4);
            await WaitFor(() => !Main.Menu.Motion.Running && !pages.Turning, 5000);
            await Wait(500);
            await Shot(ids[i], $"Settings: {sec}", $"cover's Settings by Enter, section {sec} by SettingsPages.ShowSection");
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
            await WaitFor(() => Main.Menu.CoverShown && !Main.Menu.Motion.Running, 20000);
        }

        // Back to the game.
        Main.Menu.ResumeButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(await WaitFor(() => !Main.Menu.IsPaused, 30000), "Resume opened the book");
        await Frames(3);
        await FollowView();
    }
}
