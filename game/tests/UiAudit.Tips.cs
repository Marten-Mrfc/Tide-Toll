using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 3, L2 fix (reviews: "no tooltip is pictured"): the play tooltip over a few owners, u42..u45. The pointer is put
/// on the control, the tour waits until the tooltip shows (0.25 s and its 0.12 s fade, at whatever frame rate), then
/// the picture; the pointer goes back to the bottom edge after.
/// </summary>
public partial class UiAudit
{
    private async Task TipShot(string id, string title, Control? owner, string what, bool leave = true)
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
        if (!leave) return;
        Input.WarpMouse(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        TestInput.Motion(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        await Frames(3);
    }

    /// <summary>Sheet v3: the row's own states, u46 (pointer on an affordable row: band and tooltip card), u47 (the button
    /// held on it: pressed; let go off the row, so nothing is bought) and u48 (pointer on a blocked row).</summary>
    private async Task RowStates()
    {
        if (!Wanted("u46") && !Wanted("u47") && !Wanted("u48")) return;
        int a = RowOf(ShopRow.PillState.Affordable);
        if (a < 0) { await Fast(t => { var it = new System.Collections.Generic.List<ShopItem>(); t.ShopItems(it); foreach (var x in it) if (x.Affordable) return true; return false; }, 5); await Frames(30); a = RowOf(ShopRow.PillState.Affordable); }
        if (a >= 0)
        {
            var row = Main.Hud.Marks[a];
            await TipShot("u46", "Shop row: hover on an affordable row", row, $"row '{Main.Hud.RowText(a)}'", leave: false);
            if (Wanted("u47"))
            {
                Main.Driver.SetPaused(true);
                var c = TestInput.Centre(row);
                TestInput.Button(c, MouseButton.Left, true);
                await Frames(4);
                await Shot("u47", "Shop row: pressed", $"left button held on row '{Main.Hud.RowText(a)}' (the town held still)");
                var away = new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8);
                Input.WarpMouse(away);
                TestInput.Motion(away);
                await Frames(2);
                TestInput.Button(away, MouseButton.Left, false);
                Main.Driver.SetPaused(false);
                await Frames(3);
            }
        }
        else { NotMade("u46", "Shop row: hover", "no affordable row"); NotMade("u47", "Shop row: pressed", "no affordable row"); }
        await BlockedRowShot();
        Input.WarpMouse(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        TestInput.Motion(new Vector2(GetWindow().Size.X / 2f, GetWindow().Size.Y - 8));
        await Frames(3);
    }

    private bool _blockedShot;

    /// <summary>u48: the pointer on a blocked row (taken at the first state that has one: late first age or the second age).</summary>
    private async Task BlockedRowShot()
    {
        if (_blockedShot || !Wanted("u48")) return;
        int b = RowOf(ShopRow.PillState.Blocked);
        if (b < 0) { GD.Print("UIAUDIT note: no blocked row yet for u48"); return; }
        _blockedShot = true;
        await TipShot("u48", "Shop row: pointer on a blocked row", Main.Hud.Marks[b], $"row '{Main.Hud.RowText(b)}'");
    }

    private int RowOf(ShopRow.PillState st)
    {
        for (int i = 0; i < Main.Hud.ShownItems.Count; i++) if (Main.Hud.RowPill(i) == st && Main.Hud.RowInView(i)) return i;
        return -1;
    }
}
