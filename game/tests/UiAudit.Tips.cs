using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 3, L2 fix (reviews: "no tooltip is pictured"): the play tooltip over a few owners, u42..u45. The pointer is put
/// on the control, the tour waits until the tooltip shows (0.25 s and its 0.12 s fade, at whatever frame rate), then
/// the picture; the pointer goes back to the bottom edge after.
/// </summary>
public partial class UiAudit
{
    private async Task TipShot(string id, string title, Control? owner, string what)
    {
        if (!Wanted(id)) return;
        if (owner == null || !owner.IsVisibleInTree()) { NotMade(id, title, what + ": not on screen"); return; }
        // The tooltip reads the pointer's real place (the window's), so the pointer is moved there, not only told.
        Input.WarpMouse(TestInput.Centre(owner));
        TestInput.Motion(TestInput.Centre(owner));
        bool shown = await WaitFor(() => Main.Hud.TipText.Length > 0, 20000);
        await Wait(400);
        await Frames(6);
        await Shot(id, title, $"{what}; pointer on its centre; tooltip shown={shown} text='{Main.Hud.TipText}'");
        Input.WarpMouse(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        TestInput.Motion(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        await Frames(3);
    }
}
