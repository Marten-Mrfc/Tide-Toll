using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Screenshots of the town at the window's size (run under xvfb with --resolution): the first minute, the first
/// purchase (bought through its bookmark), level 3, Pave mode with a road hovered, and the end of the age (the
/// simulation's best-payback bot plays the town between shots). Files: town-{first-minute,first-purchase,level-3,
/// pave,level-6,end-of-age,zoom-1..4,summary}-WxH.png in --out-dir (default build/screenshots). The zoom steps go
/// from the end-of-age town (tilted) to the whole book (straight down); the summary is the left page close up.
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
        // --menu-only: a short town, then only the pause menu's shots (quick look while working on the menu).
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--menu-only") >= 0) { Main.Driver.RunBot(bot, t => t.Level >= 3, 20 * 60 * 10); await MenuShots(); return; }

        // The first minute, as a player sees it.
        Main.Driver.Advance(58);
        await Settle();
        await Shot("first-minute");

        // The first purchase, through its bookmark.
        await Until(() => FirstAffordable() != null, 60 * 120, "something affordable");
        var mark = FirstAffordable()!;
        long p0 = T.Purchases;
        await Click(TestInput.Centre(mark));
        Check(T.Purchases == p0 + 1, "the first purchase");
        await Frames(10);
        await Shot("first-purchase");

        // Level 3: the bot plays on; the drawing catches up (reveals and pops) before the shot.
        Main.Driver.RunBot(bot, t => t.Level >= 3, 20 * 60 * 10);
        await Settle();
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

        // About level 6, the follow view.
        Main.Driver.RunBot(bot, t => t.Level >= 6, 20 * 60 * 30);
        Main.Rig.UserMoved = false;
        await Settle();
        await Shot("level-6");

        // The end of the age.
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60);
        Check(T.HasCharter, "the bot reached the charter");
        Main.Rig.UserMoved = false;
        await Settle();
        await Shot("end-of-age");

        // Four steps of the zoom out to the book, then the summary page close enough to read.
        var pivot = Main.Rig.PivotLocal;
        float town = Main.Rig.Camera.Size;
        float[] sizes = { Mathf.Lerp(town, Main.SlimSize, 0.5f), Main.SlimSize * 0.97f, Main.Rig.PlaySize, Main.Rig.MaxSize };
        for (int i = 0; i < sizes.Length; i++)
        {
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(pivot, sizes[i], instant: true);
            await Frames(8);
            await Shot($"zoom-{i + 1}");
        }
        var left = Main.MapOrigin.GlobalTransform.AffineInverse() * Main.GetNode<Node3D>("Book/LeftPage").GlobalPosition;
        Main.Rig.LookAt(left, Main.Rig.PlaySize * 1.08f, instant: true);
        await Frames(40);
        await Shot("summary");

        await MenuShots();
        Notes.Add($"level={T.Level} villagers={T.VillagerCount} time={T.Time:0}s dir={_dir}");
    }

    private async Task MenuShots()
    {
        // The pause menu: the book half closed, the cover menu, the four settings pages opened from the cover, the
        // New town question.
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        Main.Menu.Pause();
        Main.Cover.Hold(0.3f);
        var t1 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t1 < 1500) await Frames(1);
        await Shot("book-half-closed");
        Main.Cover.Close();
        await Until(() => Main.Menu.CoverShown, 2000, "the cover menu");
        await Frames(5);
        await Shot("cover-menu");
        foreach (var section in Settings.SettingsPages.Sections)
        {
            Main.Menu.SettingsButton.GrabFocus();
            Main.Menu.OpenSettings();
            await Frames(3);
            Main.Menu.Pages!.ShowSection(section);
            await Frames(10);
            await Shot("settings-" + section.ToLowerInvariant());
            Main.Menu.Pages!.Close();
            await Frames(5);
        }
        Main.Menu.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(5);
        Check(Main.Menu.QuestionShown, "the New town question");
        await Shot("new-town-question");
        await Key(Godot.Key.Escape);
        await Frames(3);
    }

    /// <summary>Waits until new roads have drawn themselves in, new buildings have popped in and the camera has eased.</summary>
    private async Task Settle()
    {
        await Frames(3);
        await Until(() =>
        {
            for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r) && Main.Ground.RoadReveal(r) < 1f) return false;
            return Main.Buildings.Popping == 0;
        }, 2000, "the drawing to catch up");
        // The camera eases about 12 per second: a second of real time brings it within a few percent.
        var t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < 1500) await Frames(1);
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
