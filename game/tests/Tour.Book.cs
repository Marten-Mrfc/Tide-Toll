using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>The tour at the end of the first age: the zoom from the tilted close view out to the book, pausing by
/// closing the book, the cover menu, New town's question, the settings pages and the largest interface size.
/// Every setting changed is put back (checked).</summary>
public partial class Tour
{
    private async Task BookAndMenus()
    {
        await FollowView();
        await Settle(600);
        var pivot = Main.Rig.PivotLocal;
        float play = Main.Rig.Camera.Size;

        // g01: the closest view (tilted), on the middle of the town.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(pivot, Main.Rig.MinSize, instant: true);
        await Settle(1200);
        await Calm();
        await Shot("g01-close");

        // g02: one wheel step out from the follow view's size: the whole town (the normal view).
        Main.Rig.LookAt(pivot, play, instant: true);
        await Wait(1200);
        await Shot("g02-zoom-1");

        // g03: the whole page, the interface thinned to the strip.
        Main.Rig.LookAt(pivot, Main.Rig.PlaySize, instant: true);
        await WaitFor(() => Main.Hud.SlimVisible && Main.Hud.FullAlpha <= 0f, 5000);
        await Wait(600);
        if (!Main.Hud.SlimVisible) Wrong("g03-zoom-2", "The slim strip was not showing.");
        await Shot("g03-zoom-2");

        // g04: fully out, the open book.
        Main.Rig.LookAt(pivot, Main.Rig.MaxSize, instant: true);
        await Wait(1500);
        await Shot("g04-book");

        // g05: pausing (Esc): the book half closed.
        Main.Rig.LookAt(pivot, play, instant: true);
        Main.Rig.UserMoved = false;
        await Wait(800);
        Main.Menu.Pause();
        Main.Cover.Hold(0.45f);
        await Wait(1500);
        await Shot("g05-closing");
        Main.Cover.Close();
        await WaitFor(() => Main.Menu.CoverShown, 20000);
        await Wait(400);
        Check(Main.Menu.CoverShown, "g06: the cover menu shows");
        await Shot("g06-cover");

        // h01..h04: the settings pages from the cover's Settings.
        var store = Main.Settings.Store;
        int ui0 = store.InterfaceSize;
        Key pave0 = store.KeyFor("tt_pave", 0), bell0 = store.KeyFor("tt_bell", 0);
        string[] ids = { "h01-sound", "h02-display", "h03-keys", "h04-saving" };
        for (int i = 0; i < SettingsPages.Sections.Length && i < ids.Length; i++)
        {
            Main.Menu.SettingsButton.GrabFocus();
            await Key(Godot.Key.Enter);
            await WaitFor(() => Main.Menu.Pages != null, 5000);
            var pages = Main.Menu.Pages!;
            pages.ShowSection(SettingsPages.Sections[i]);
            await Frames(4);
            if (SettingsPages.Sections[i] == "Keys")
            {
                // Rebind Pave by clicking its key and pressing Space, the bell's key: the clash asks what to do.
                await Click(TestInput.Centre(pages.Controls["keys.tt_pave.0"]));
                await Key(Godot.Key.Space);
                await Frames(4);
                if (pages.Slip != "clash") Wrong("h03-keys", "No clash was shown for Space.");
                await Wait(500);
                await Shot(ids[i]);
                await Key(Godot.Key.Escape); // cancels the clash: nothing changes
                await Frames(3);
                Check(store.KeyFor("tt_pave", 0) == pave0 && store.KeyFor("tt_bell", 0) == bell0, "the cancelled clash changed a key");
            }
            else
            {
                await Wait(500);
                await Shot(ids[i]);
            }
            await Key(Godot.Key.Escape); // "Esc always goes back from these pages."
            await WaitFor(() => Main.Menu.CoverShown, 5000);
        }

        // g07: New town asks once; No keeps the town.
        Main.Menu.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(4);
        Check(Main.Menu.QuestionShown, "the New town question");
        await Wait(400);
        await Shot("g07-new-town");
        await Key(Godot.Key.Escape);
        await Frames(4);
        Check(Main.Driver.Seed == 1 && T.HasCharter, "the town was kept");

        // h05: interface 150 % (Display page), back in the game; then put back.
        await SetInterface(150);
        await Resume();
        await FollowView();
        await Settle(1000);
        await Calm();
        await Shot("h05-big");
        Main.Menu.Pause();
        await WaitFor(() => Main.Menu.CoverShown, 20000);
        await SetInterface(ui0);
        await Resume();
        Check(store.InterfaceSize == ui0 && Math.Abs(GetWindow().ContentScaleFactor - ui0 / 100f) < 1e-3f, "the interface size was put back");
        await FollowView();
    }

    private async Task SetInterface(int percent)
    {
        Main.Menu.SettingsButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await WaitFor(() => Main.Menu.Pages != null, 5000);
        var pages = Main.Menu.Pages!;
        pages.ShowSection("Display");
        await Frames(3);
        ((ChoiceButton)pages.Controls["display.interface"]).SetIndex((percent - GameSettings.MinInterface) / 10, notify: true);
        await Frames(4);
        // Back by Esc: at another interface size the control's rect is no longer in window pixels.
        await Key(Godot.Key.Escape);
        Check(await WaitFor(() => Main.Menu.CoverShown, 5000), "Esc went back from the settings to the cover");
    }

    private async Task Resume()
    {
        Main.Menu.ResumeButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(await WaitFor(() => !Main.Menu.IsPaused, 30000), "Resume opened the book");
        await Frames(3);
    }
}
