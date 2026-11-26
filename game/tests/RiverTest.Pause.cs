using System;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 5: pausing in the second age, with the view widened to the riverside, by the real Esc key: the closed cover is
/// whole and centred on screen, the settings open from it and close back to it, and Resume returns to the river view.
/// </summary>
public partial class RiverTest
{
    private async Task PauseInTheSecondAge()
    {
        if (Main.Hud.AgePageOpen) { await Key(Godot.Key.Escape); await Frames(3); }
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(20);
        var pivot = Main.Rig.PivotLocal; float size = Main.Rig.Camera.Size;
        await Key(Godot.Key.Escape);
        Check(Main.Menu.IsPaused, "Esc pauses in the second age");
        await Until(() => Main.Menu.CoverShown, 3000, "the cover menu in the second age");
        await Frames(3);
        var vp = Main.GetViewport().GetVisibleRect().Size;
        var r = Main.Menu.CoverRect();
        Check(r.Position.X >= -1 && r.Position.Y >= -1 && r.End.X <= vp.X + 1 && r.End.Y <= vp.Y + 1, $"the closed cover is whole on screen ({r}, screen {vp})");
        Check(Math.Abs(r.GetCenter().X - vp.X / 2) < vp.X * 0.05f && Math.Abs(r.GetCenter().Y - vp.Y / 2) < vp.Y * 0.05f, $"the closed cover is centred ({r.GetCenter()}, screen {vp})");
        var resume = Main.Menu.ResumeButton.GetGlobalRect();
        Check(r.Encloses(resume), "Resume lies on the cover");
        Main.Menu.SettingsButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(5);
        Check(Main.Menu.Pages != null && Main.Menu.Now == PauseMenu.State.Settings, "the settings open from the cover in the second age");
        await Key(Godot.Key.Escape);
        await Frames(5);
        Check(Main.Menu.CoverShown, "Esc on the settings goes back to the cover");
        await Click(TestInput.Centre(Main.Menu.ResumeButton));
        await Until(() => !Main.Menu.IsPaused, 3000, "the book to open again");
        await Frames(60);
        Check(Main.Rig.PivotLocal.DistanceTo(pivot) < 2f && Math.Abs(Main.Rig.Camera.Size - size) < 2f,
            $"Resume returns to the river view (pivot {Main.Rig.PivotLocal} vs {pivot}, size {Main.Rig.Camera.Size:0.0} vs {size:0.0})");
        Notes.Add($"pause2=ok cover={r.GetCenter().X:0},{r.GetCenter().Y:0}");
        var rp = T.RiverPlan!;
        var lanes = new System.Text.StringBuilder();
        for (int i = 0; i < rp.Links.Length; i++)
            if (rp.Links[i].Kind == TideToll.Sim.Town.RiverLinkKind.Lane)
                lanes.Append($"{i}:L{rp.Links[i].Level}/{(T.RiverLinkOpen(i) ? "open" : "shut")}/r{Main.Ground.LaneReveal(i):0.0}/w{Main.Ground.LaneShown(i):0.00} ");
        Notes.Add("lanes=" + lanes.ToString().Trim().Replace(' ', ','));
    }
}
