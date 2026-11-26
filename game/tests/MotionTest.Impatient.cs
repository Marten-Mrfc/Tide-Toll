using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// Impatient input (round 7): Esc, Enter, clicks and tab changes in the middle of transitions. Each case is sampled
/// like the staged ones (continuity, no Visible flip of anything on screen, no hitch) and must end at rest in a state
/// that is exactly right (Settled): never the book half open, the interface missing, the game paused with no menu or
/// the input locked.
/// </summary>
public partial class MotionTest
{
    private int At(float seconds) => Math.Max(1, (int)MathF.Round(seconds * _fps));

    private async Task Impatient(Vector3 pivot0, float size0)
    {
        // Esc while the book closes: it opens again from where it is, to exactly the view kept.
        int esc2 = At(0.45f);
        await Record(i => { if (i == 0 || i == esc2) TestInput.Key(Godot.Key.Escape); });
        Analyse("esc-during-close", Kind.Impatient);
        Settled("esc-during-close");
        Check(M.Now == PauseMenu.State.Playing, "Esc during the closing did not open the book again");
        Check(Main.Rig.PivotLocal.DistanceTo(pivot0) < 1e-3f && MathF.Abs(Main.Rig.Camera.Size - size0) < 1e-3f, "after Esc during the closing the camera is not back");
        await Frames(3);
        Check(M.MenuSaves > 0, "a pause turned round before its end was never saved");

        // Esc while the book opens: it closes again.
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.CoverShown, 900, "the cover");
        int esc3 = At(0.3f);
        await Record(i => { if (i == 0 || i == esc3) TestInput.Key(Godot.Key.Escape); });
        Analyse("esc-during-open", Kind.Impatient);
        Settled("esc-during-open");
        Check(M.Now == PauseMenu.State.Cover, "Esc during the opening did not close the book again");

        // Esc while the settings spread arrives: back to the cover.
        M.SettingsButton.GrabFocus();
        int back = At(0.3f);
        await Record(i => { if (i == 0) TestInput.Key(Godot.Key.Enter); if (i == back) TestInput.Key(Godot.Key.Escape); });
        Analyse("back-during-settings-open", Kind.Impatient);
        Settled("back-during-settings-open");
        Check(M.Now == PauseMenu.State.Cover && GetViewport().GuiGetFocusOwner() == M.SettingsButton, "Esc during the settings' opening did not go back to the cover on Settings");

        // Tabs mid-turn: back to the one being left (the leaf turns back), then a third chosen mid-turn (it waits).
        TestInput.Key(Godot.Key.Enter);
        await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 900, "the settings");
        await Frames(2);
        int again = At(0.12f);
        await Record(i => { if (i == 0) ClickNow(P.Controls["ribbon.Display"]); if (i == again) ClickNow(P.Controls["ribbon.Sound"]); });
        Analyse("tab-turned-back", Kind.Impatient);
        Settled("tab-turned-back");
        Check(P.Section == "Sound" && P.SectionShown(0) == 1f && P.FocusedName() == "ribbon.Sound", "a tab chosen back mid-turn did not end on it");
        await Record(i => { if (i == 0) ClickNow(P.Controls["ribbon.Keys"]); if (i == again) ClickNow(P.Controls["ribbon.Saving"]); });
        Analyse("tab-redirected", Kind.Impatient);
        Settled("tab-redirected");
        Check(P.Section == "Saving" && P.SectionShown(3) == 1f && P.FocusedName() == "ribbon.Saving", "a third tab chosen mid-turn did not end on it");
        // Flicking through the tabs by keyboard, a key every other frame.
        await Record(i => { if (i < 12) TestInput.Key(i % 2 == 0 ? Godot.Key.Up : Godot.Key.Enter); }, 900);
        Analyse("tab-flick", Kind.Impatient);
        Settled("tab-flick");
        Check(P.Section == "Sound" && P.SectionShown(0) == 1f, $"flicking up through the tabs ended on {P.Section}");
        // A click on the pages mid-turn lands the page at once and the click finds its control: Back, which closes the book.
        int backClick = At(0.08f);
        await Record(i => { if (i == 0) ClickNow(P.Controls["ribbon.Display"]); if (i == backClick) ClickNow(P.Controls["back"]); });
        Analyse("click-mid-turn", Kind.Impatient);
        Settled("click-mid-turn");
        Check(M.Now == PauseMenu.State.Cover, "a click on Back mid-turn did not reach it");

        // Esc on the way back from the settings: on to the town (Esc on the cover would do the same).
        M.SettingsButton.GrabFocus();
        TestInput.Key(Godot.Key.Enter);
        await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 900, "the settings");
        await Frames(2);
        int esc4 = At(0.3f);
        await Record(i => { if (i == 0 || i == esc4) TestInput.Key(Godot.Key.Escape); });
        Analyse("esc-during-return", Kind.Impatient);
        Settled("esc-during-return");
        Check(M.Now == PauseMenu.State.Playing, "Esc on the way back from the settings did not resume");
        Check(Main.Rig.PivotLocal.DistanceTo(pivot0) < 1e-3f && MathF.Abs(Main.Rig.Camera.Size - size0) < 1e-3f, "after Esc on the way back the camera is not back");

        // Enter on Resume while the lettering is still coming in: the book opens again from there.
        bool sent = false;
        await Record(i =>
        {
            if (i == 0) TestInput.Key(Godot.Key.Escape);
            else if (!sent && M.Now == PauseMenu.State.Closing && M.LetteringVisible && M.LetteringAlpha > 0.3f) { sent = true; TestInput.Key(Godot.Key.Enter); }
        });
        Analyse("enter-on-lettering", Kind.Impatient);
        Settled("enter-on-lettering");
        Check(sent && M.Now == PauseMenu.State.Playing, "Enter on Resume while the lettering came in did not resume");

        // A click on Settings while the lettering is coming in: the closing ends and the settings open.
        sent = false;
        await Record(i =>
        {
            if (i == 0) TestInput.Key(Godot.Key.Escape);
            else if (!sent && M.Now == PauseMenu.State.Closing && M.LetteringVisible && M.LetteringAlpha > 0.3f) { sent = true; ClickNow(M.SettingsButton); }
        });
        Analyse("click-settings-on-lettering", Kind.Impatient);
        Settled("click-settings-on-lettering");
        Check(sent && M.Now == PauseMenu.State.Settings, "a click on Settings while the lettering came in did not open the settings");
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.CoverShown, 900, "the cover");

        // The question: Esc as it comes in, and Enter on Keep this town.
        M.NewTownButton.GrabFocus();
        int q = At(0.1f);
        await Record(i => { if (i == 0) TestInput.Key(Godot.Key.Enter); if (i == q) TestInput.Key(Godot.Key.Escape); });
        Analyse("esc-during-question", Kind.Impatient);
        Settled("esc-during-question");
        Check(M.Now == PauseMenu.State.Cover, "Esc as the question came in did not go back to the cover");
        int seed = Main.Driver.Seed;
        await Record(i => { if (i == 0) TestInput.Key(Godot.Key.Enter); if (i == q) TestInput.Key(Godot.Key.Enter); });
        Analyse("keep-during-question", Kind.Impatient);
        Settled("keep-during-question");
        Check(M.Now == PauseMenu.State.Cover && Main.Driver.Seed == seed, "Keep this town as the question came in");

        // A click on the table beside the closing book does nothing; then Esc pressed on six frames running.
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.Now == PauseMenu.State.Playing && !M.Motion.Running, 900, "the town");
        await Record(i => { if (i == 0) TestInput.Key(Godot.Key.Escape); if (i == 8) { TestInput.Motion(new Vector2(60, 1000)); TestInput.Button(new Vector2(60, 1000), MouseButton.Left, true); TestInput.Button(new Vector2(60, 1000), MouseButton.Left, false); } });
        Analyse("click-while-closing", Kind.Impatient);
        Settled("click-while-closing");
        Check(M.Now == PauseMenu.State.Cover, "a click beside the closing book changed what happened");
        await Record(i => { if (i < 6) TestInput.Key(Godot.Key.Escape); });
        Analyse("esc-spam", Kind.Impatient);
        Settled("esc-spam");
        if (M.Now != PauseMenu.State.Playing)
        {
            TestInput.Key(Godot.Key.Escape);
            await Rest();
        }
        Settled("the end");
        Check(M.Now == PauseMenu.State.Playing, "the test ends in the town");
        Notes.Add("impatient=ok");
    }
}
