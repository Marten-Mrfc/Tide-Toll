using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-book, at 1920 x 1080 through real input: at the normal view the camera is tilted and a villager stands at
/// least <see cref="MinVillagerPx"/> pixels tall; the wheel zooms toward the cursor; a Pave click on a road lands on
/// that road while tilted; then wheel notches at the centre zoom out step by step to the whole book: the pitch only
/// flattens and ends straight down, the interface goes from full to the slim strip to the book stage, the summary page's
/// numbers match the simulation and the simulation keeps running; Home brings back the tilted town view.
/// </summary>
public partial class BookTest : TownTestBase
{
    protected override string Marker => "BOOK PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    /// <summary>Smallest on-screen height of a villager at the normal view, 1920 x 1080.</summary>
    public const float MinVillagerPx = 28f;

    protected override async Task Run()
    {
        Main.Driver.Advance(30);
        await Settle();
        Check(Main.Stage == Main.ZoomStage.Town, "starts on the town");
        Check(Main.Rig.PitchNow < 70f, $"the normal view is tilted (pitch {Main.Rig.PitchNow:0.0})");
        Check(Main.Hud.FullAlpha >= 1f && !Main.Hud.SlimVisible, "the whole interface shows on the town");

        // A villager reads as a person: feet to head on screen.
        float tallest = 0;
        for (int i = 0; i < T.VillagerCount; i++)
        {
            var p = Main.Villagers.Shown(i);
            var feet = Main.Rig.LocalToScreen(new Vector3(p.X * TownGround.Cell, 0, p.Y * TownGround.Cell));
            var head = Main.Rig.LocalToScreen(new Vector3(p.X * TownGround.Cell, Main.Villagers.ModelHeight * Main.Villagers.WalkerScale, p.Y * TownGround.Cell));
            tallest = Math.Max(tallest, feet.DistanceTo(head));
        }
        Check(tallest >= MinVillagerPx, $"a villager is at least {MinVillagerPx} px tall at the normal view ({tallest:0.0} px)");
        Notes.Add($"villager_px={tallest:0.0} pitch={Main.Rig.PitchNow:0.0} size={Main.Rig.Camera.Size:0.0}");

        // Zoom toward the cursor: the map point under it stays under it.
        var cursor = new Vector2(1250, 400);
        Check(Main.Rig.PickLocal(cursor, out var before), "pick under the cursor");
        await Wheel(cursor, MouseButton.WheelUp);
        await Until(() => Math.Abs(Main.Rig.Camera.Size - Main.Rig.TargetSize) < 0.01f, 600, "the zoom in eased");
        Check(Main.Rig.PickLocal(cursor, out var after), "pick under the cursor again");
        Check(before.DistanceTo(after) < 0.6f, $"the wheel zooms toward the cursor (moved {before.DistanceTo(after):0.00} m)");
        await Key(Godot.Key.Home);
        await Settle();

        await PaveWhileTilted();

        // Zoom out notch by notch to the book.
        Main.Rig.UserMoved = false;
        await Key(Godot.Key.Home);
        await Settle();
        var centre = new Vector2(960, 540);
        float lastPitch = Main.Rig.PitchNow, firstPitch = lastPitch;
        bool sawSlim = false, sawPage = false, sawBook = false, slimBeforeBook = false;
        double time0 = T.Time;
        int notches = 0;
        while (notches < 40 && !(Main.Stage == Main.ZoomStage.Book && Main.Rig.TargetSize >= Main.Rig.MaxSize - 0.01f && Math.Abs(Main.Rig.Camera.Size - Main.Rig.TargetSize) < 0.05f))
        {
            float pitchBefore = Main.Rig.PitchNow;
            await Wheel(centre, MouseButton.WheelDown);
            notches++;
            for (int f = 0; f < 40; f++)
            {
                await Frames(1);
                Check(Main.Rig.PitchNow >= lastPitch - 0.01f, $"the camera only flattens while zooming out ({lastPitch:0.00} then {Main.Rig.PitchNow:0.00})");
                lastPitch = Main.Rig.PitchNow;
                if (Main.Stage == Main.ZoomStage.Page) sawPage = true;
                if (Main.Hud.SlimVisible && Main.Hud.FullAlpha <= 0f) { sawSlim = true; if (!sawBook) slimBeforeBook = true; }
                if (Main.Stage == Main.ZoomStage.Book) sawBook = true;
            }
            // Round 4: the first scroll is not flat: each of the first four notches tilts the view too.
            if (notches <= 4) Check(Main.Rig.PitchNow - pitchBefore > 0.8f, $"notch {notches} out changed the tilt only {Main.Rig.PitchNow - pitchBefore:0.00} degrees");
        }
        Check(MathF.Abs(Main.Rig.PitchFor(Main.Rig.MinSize) - 50f) < 1.5f, $"the closest view looks down at {Main.Rig.PitchFor(Main.Rig.MinSize):0.0} degrees, not about 50");
        Check(sawBook, $"the wheel reaches the book ({notches} notches, size {Main.Rig.Camera.Size:0})");
        Check(sawPage && sawSlim && slimBeforeBook, "the interface thins to the slim strip on the way out");
        Check(firstPitch < 75f && Main.Rig.PitchNow > 89.99f, $"tilted on the town ({firstPitch:0.0}), straight down at the book ({Main.Rig.PitchNow:0.00})");
        Check(Main.Hud.SlimVisible && Main.Hud.FullAlpha <= 0f, "the slim strip shows over the book");
        Check(Main.Hud.SlimText.Contains(TownHud.Money(T.Coin) + " coin") && Main.Hud.SlimText.Contains($"Level {T.Level}"), $"the slim strip shows coin and level ({Main.Hud.SlimText})");
        Check(T.Time > time0 + 1, "the simulation keeps running while zoomed out");
        // The book view shows the whole book: its corners are on screen.
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var corner = Main.Rig.Camera.UnprojectPosition(new Vector3(sx * BookBuilder.CoverSize.X / 2f, 0, sz * BookBuilder.CoverSize.Y / 2f));
                Check(corner.X >= -2 && corner.X <= 1922 && corner.Y >= -2 && corner.Y <= 1082, $"the book's corner is in view ({corner})");
            }

        // The summary page matches the simulation (paused so the numbers hold still while compared).
        Main.Driver.SetPaused(true);
        await WaitFrames(45);
        string want = string.Join("\n", new[]
        {
            T.VillagerCount.ToString(), TownSummary.Cottages(T).ToString(), TownSummary.OpenRoads(T).ToString(), T.CountPaved().ToString(),
            TownHud.Money(T.CoinEarned), T.Trips.ToString(), Minutes(T.Time), $"{TownSummary.Achieved(T)} of {T.AchievementCount}",
        });
        Check(Main.Summary.ValuesText == want, $"the summary's numbers match the simulation:\n{Main.Summary.ValuesText}\nwanted\n{want}");
        Check(Main.Summary.NameText == TownName.Of(T.Seed) && Main.Summary.NameText.Length >= 4, "the summary carries the town's name");
        Check(Main.Summary.AgeText == $"The first age, level {T.Level} of {T.MaxLevel}", "the summary shows the age and level");
        Check(Main.Summary.NextText == T.NextGift, "the summary shows the next gift");
        Main.Driver.SetPaused(false);
        int rewrites = Main.Summary.Rewrites;
        await Until(() => Main.Summary.Rewrites > rewrites, 600, "the summary updates while watched");
        Notes.Add($"notches={notches} town={Main.Summary.NameText}");

        // Home returns to the town.
        await Key(Godot.Key.Home);
        await Settle();
        Check(Main.Stage == Main.ZoomStage.Town && Main.Following, "Home returns to the town view");
        Check(Main.Rig.PitchNow < 75f, "tilted again on the town");
        Check(Main.Hud.FullAlpha >= 1f && !Main.Hud.SlimVisible, "the whole interface is back");
        int hints = 0;
        foreach (var n in Main.Hud.NoteLog) if (n == TownHud.ScrollHint) hints++;
        Check(hints <= 1, "the hint to scroll out comes at most once");
        Notes.Add($"hint={hints}");
    }

    private static string Minutes(double seconds)
    {
        long n = (long)(seconds / 60.0);
        return n >= 60 ? $"{n / 60} h {n % 60:00} min" : $"{n} min";
    }

    private async Task PaveWhileTilted()
    {
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.SuggestedPave() >= 0 && t.Coin >= t.PavePrice(t.SuggestedPave()), 20 * 60 * 20);
        Check(T.SuggestedPave() >= 0, "a road can be paved");
        await Settle();
        Main.Driver.SetPaused(true);
        Check(Main.Rig.PitchNow < 85f, $"still tilted for the Pave click (pitch {Main.Rig.PitchNow:0.0})");
        int r = T.SuggestedPave();
        await Key(Godot.Key.P);
        Check(Main.Hud.PaveMode, "P switches Pave mode on");
        var road = T.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        var at = Main.Hud.RoadScreen(r);
        Check(Main.Rig.PickLocal(at, out var l), "the road's screen point picks the ground");
        float off = new Vector2(l.X / TownGround.Cell - (float)x, l.Z / TownGround.Cell - (float)y).Length();
        Check(off < 0.05f, $"picking under the tilt finds the same ground point ({off:0.000} cells off)");
        TestInput.Motion(at);
        await Frames(3);
        Check(Main.Hud.HoverRoad == r, $"hovering the road picks it (hover {Main.Hud.HoverRoad}, road {r})");
        var s0 = T.RoadInfo(r).Surface;
        await Click(at);
        Check(T.RoadInfo(r).Surface != s0, "a Pave click on the road paved that road while tilted");
        if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
        Main.Driver.SetPaused(false);
        Notes.Add($"paved={r}");
    }

    private async Task Wheel(Vector2 at, MouseButton b)
    {
        TestInput.Motion(at);
        await Frames(1);
        TestInput.Button(at, b, true);
        TestInput.Button(at, b, false);
        await Frames(1);
    }

    private async Task WaitFrames(int n) => await Frames(n);

    /// <summary>Lets the camera ease (headless frames are fast: wait on frames, then until the size settles).</summary>
    private async Task Settle()
    {
        await Frames(20);
        await Until(() => Math.Abs(Main.Rig.Camera.Size - Main.Rig.TargetSize) < 0.05f, 2000, "the camera to settle");
        await Frames(5);
    }
}
