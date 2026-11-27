using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n8: the before/after pictures for the owner (--ink-board, into --out-dir): every kind in InkCeilings at the play
/// view in a 1920 x 1080 window (the middle 640 x 480, 960 x 480 for the street), the owner's cottage (a2) at the
/// start view in windows of 720, 900, 1080 and 1440 lines (the same ground at each), and the town the bot plays to
/// the charter at the start view, whole frame. tools/ink-lab/before-after.py puts two such runs side by side.
/// </summary>
public partial class AssetAudit
{
    private async Task InkBoard(float play1080, float walker)
    {
        GetWindow().Size = new Vector2I(1920, 1080);
        await Frames(8);
        foreach (var (name, _) in InkCeilings)
        {
            if (_only != null && !_only.Contains(name)) continue;
            var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
            var subject = InkSubject(name, walker);
            subject.Position = at;
            Main.MapOrigin.AddChild(subject);
            var c = Bounds(subject).GetCenter();
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, play1080, 0f), play1080);
            Main.Rig.UserMoved = true;
            await Frames(4);
            var img = GetViewport().GetTexture().GetImage();
            int cw = name == "street" ? 960 : 640;
            img.GetRegion(new Rect2I(960 - cw / 2, 300, cw, 480)).SavePng(System.IO.Path.Combine(_dir, $"board-{name}.png"));
            subject.QueueFree();
            await Frames(1);
        }
        {
            var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
            var subject = InkSubject("cottage-a2", walker);
            subject.Position = at;
            Main.MapOrigin.AddChild(subject);
            var c = Bounds(subject).GetCenter();
            foreach (int hgt in new[] { 720, 900, 1080, 1440 })
            {
                GetWindow().Size = new Vector2I(hgt * 16 / 9, hgt);
                await Frames(6);
                Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, play1080, 0f), play1080);
                Main.Rig.UserMoved = true;
                await Frames(4);
                var img = GetViewport().GetTexture().GetImage();
                int cw = 320 * hgt / 1080, ch = 240 * hgt / 1080;
                img.GetRegion(new Rect2I(hgt * 16 / 18 - cw / 2, hgt / 2 - ch / 2, cw, ch)).SavePng(System.IO.Path.Combine(_dir, $"board-window-{hgt}.png"));
            }
            subject.QueueFree();
        }
        if (_noTown) return;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = true;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Life") is { } life) life.Visible = true;
        GetWindow().Size = new Vector2I(1920, 1080);
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60 * 12);
        Main.Driver.SetPaused(true);
        await Frames(10);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        var target = Main.Rig.TargetPivot;
        Main.Rig.UserMoved = true;
        Main.Rig.Place(target, play1080);
        await Frames(6);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_dir, "board-town.png"));
    }
}
