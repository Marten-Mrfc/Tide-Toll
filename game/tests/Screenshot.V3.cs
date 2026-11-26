using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 5: rules v3 on screen (-- --v3-only, or in the full run): the choice of two (town-choice), the boon's moment
/// (town-boon), a touch beside a cottage (town-touch), an extra hand's marker (town-hand), the charter drive
/// (town-drive) and the summary page with the boons (town-summary-boons). Shop lines with their worth show in each.
/// </summary>
public partial class Screenshot
{
    private async Task V3Shots()
    {
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBotUntil(bot, t => t.ChoiceOpen, 20 * 60 * 60 * 2);
        Check(T.ChoiceOpen, "the bot played to the first choice");
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Settle();
        await Shot("choice");
        Main.Hud.PickBoon(0);
        await Frames(20);
        await Shot("boon");

        // A touch beside a cottage, close.
        Main.Driver.RunBot(bot, t => CottageTouch(t) >= 0, 20 * 60 * 60 * 2);
        int k = CottageTouch(T);
        Check(k >= 0, "a touch beside a cottage");
        var v = T.Touch(k);
        await Look(v.X, v.Y, 22);
        await Shot("touch");

        // An extra hand on offer: the marker on its workplace and its bookmark.
        Main.Driver.RunBotUntil(bot, t => { for (int i = 0; i < t.WorkplaceCount; i++) if (t.HandOffered(i)) return true; return false; }, 20 * 60 * 60 * 4);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Settle();
        await Shot("hand");

        // The charter drive, part filled.
        Main.Driver.RunBot(bot, t => t.Drive.Active && t.Drive.Delivered * 10 >= t.Drive.Amount * 3, 20 * 60 * 60 * 10);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Settle();
        await Shot("drive");

        // The summary page with the boons held.
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60 * 4);
        // The level events reach Main a frame later and turn the follow view back on: let them pass first.
        Main.Driver.SetPaused(true);
        await Frames(6);
        var left = Main.MapOrigin.GlobalTransform.AffineInverse() * Main.GetNode<Node3D>("Book/LeftPage").GlobalPosition;
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(left, Main.Rig.PlaySize * 1.08f, instant: true);
        await Frames(60);
        Check(Main.Summary.BoonsText.Contains(Town.BoonName((Boon)T.ChoicePicked(0))), $"the summary lists the boons ('{Main.Summary.BoonsText}')");
        await Shot("summary-boons");
        Main.Driver.SetPaused(false);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
    }

    private static int CottageTouch(Town t)
    {
        for (int i = 0; i < t.TouchCount; i++)
        {
            var v = t.Touch(i);
            if (v.Plot >= 0 && t.Plan.Plots[v.Plot].Kind == PlotKind.Cottage) return i;
        }
        return -1;
    }

    private async Task ResumeFromMenu()
    {
        if (!Main.Menu.IsPaused) return;
        if (Main.Menu.QuestionShown) await Key(Godot.Key.Escape);
        Main.Menu.Resume();
        await Until(() => !Main.Menu.IsPaused, 2000, "the book to open again");
        await Frames(5);
    }

    /// <summary>Round 5: pausing in the second age with the view widened to the riverside: the cover whole and centred.</summary>
    private async Task RiverPauseShots()
    {
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        var pivot = Main.Rig.PivotLocal; float size = Main.Rig.Camera.Size;
        Main.Menu.Pause();
        await Until(() => Main.Menu.CoverShown, 3000, "the cover menu in the second age");
        await Frames(5);
        var vp = Main.GetViewport().GetVisibleRect().Size;
        var r = Main.Menu.CoverRect();
        Check(r.Position.X >= 0 && r.Position.Y >= 0 && r.End.X <= vp.X && r.End.Y <= vp.Y, $"the closed cover is whole on screen ({r})");
        Check(Math.Abs(r.GetCenter().X - vp.X / 2) < vp.X * 0.05f && Math.Abs(r.GetCenter().Y - vp.Y / 2) < vp.Y * 0.05f, $"the closed cover is centred ({r.GetCenter()})");
        await Shot("river-paused");
        Main.Menu.OpenSettings();
        await Frames(10);
        await Shot("river-paused-settings");
        Main.Menu.Pages!.Close();
        await Frames(5);
        Main.Menu.Resume();
        await Until(() => !Main.Menu.IsPaused, 3000, "the book to open again");
        await Frames(60);
        Check(Main.Rig.PivotLocal.DistanceTo(pivot) < 2f && Math.Abs(Main.Rig.Camera.Size - size) < 2f, "Resume returns to the river view");
        await Shot("river-resumed");
    }
}
