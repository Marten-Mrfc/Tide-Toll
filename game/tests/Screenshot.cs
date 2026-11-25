using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Screenshots of the town at the window's size (run under xvfb with --resolution): the first minute, the first
/// purchase (bought through its bookmark), level 3, Pave mode with a road hovered, and the end of the age (the
/// simulation's best-payback bot plays the town between shots). Files: town-{first-minute,first-purchase,level-3,
/// pave,end-of-age}-WxH.png in --out-dir (default build/screenshots).
/// </summary>
public partial class Screenshot : TownTestBase
{
    protected override string Marker => "SCREENSHOT PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    private string _dir = "";

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/screenshots");
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
        System.IO.Directory.CreateDirectory(_dir);
        var bot = new TownBot(BotKind.Payback, 1);

        // The first minute, as a player sees it.
        Main.Driver.SetSpeed(8);
        await Until(() => T.Time >= 60, 60 * 60, "the first minute");
        Main.Driver.SetSpeed(1);
        await Frames(30);
        await Shot("first-minute");

        // The first purchase, through its bookmark.
        await Until(() => FirstAffordable() != null, 60 * 120, "something affordable");
        var mark = FirstAffordable()!;
        long p0 = T.Purchases;
        await Click(TestInput.Centre(mark));
        Check(T.Purchases == p0 + 1, "the first purchase");
        await Frames(40);
        await Shot("first-purchase");

        // Level 3: the bot plays on; the drawing catches up (reveals and pops) before the shot.
        Main.Driver.RunBot(bot, t => t.Level >= 3, 20 * 60 * 10);
        await Frames(150);
        await Shot("level-3");

        // Pave mode with the suggested road hovered.
        Main.Driver.RunBot(bot, t => t.SuggestedPave() >= 0 && t.Coin >= t.PavePrice(t.SuggestedPave()), 20 * 60 * 10);
        Main.Driver.SetPaused(true);
        await Frames(30);
        Main.Hud.SetPaveMode(true);
        int sug = T.SuggestedPave();
        if (sug >= 0) TestInput.Motion(Main.Hud.RoadScreen(sug));
        await Frames(20);
        await Shot("pave");
        Main.Hud.SetPaveMode(false);
        Main.Driver.SetPaused(false);

        // The end of the age.
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60);
        Check(T.HasCharter, "the bot reached the charter");
        Main.Rig.UserMoved = false;
        await Frames(240);
        await Shot("end-of-age");
        Notes.Add($"level={T.Level} villagers={T.VillagerCount} time={T.Time:0}s dir={_dir}");
    }

    private Button? FirstAffordable()
    {
        foreach (var k in new[] { ShopItemKind.Villager, ShopItemKind.Pace, ShopItemKind.Load })
        {
            var b = Main.Hud.Mark(k);
            if (b != null && !b.Disabled) return b;
        }
        return null;
    }

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        var size = GetWindow().Size;
        string path = System.IO.Path.Combine(_dir, $"town-{name}-{size.X}x{size.Y}.png");
        img.SavePng(path);
        Notes.Add(System.IO.Path.GetFileName(path));
    }
}
