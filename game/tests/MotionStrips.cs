using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Frame strips of the book's transitions for human eyes (tools/motion-strips.sh: rendered under xvfb with the Mobile
/// renderer at 1920 x 1080). Each transition is started by real input, its clock is then held at twelve evenly spaced
/// moments from its start to its end (BookMotion.Hold, SettingsPages.HoldTurnAt, TownHud.HoldPageTurnAt) and each moment
/// is saved at 640 x 360 as --out-dir/frames/NAME-NN.png; the script joins them into one contact sheet per transition.
/// </summary>
public partial class MotionStrips : TownTestBase
{
    protected override string Marker => "STRIPS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    public const int Count = 12;
    private string _dir = "";
    private PauseMenu M => Main.Menu;
    private SettingsPages P => Main.Menu.SettingsSpread;

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/motion");
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "frames"));
        bool ageOnly = Array.IndexOf(OS.GetCmdlineUserArgs(), "--age-only") >= 0;
        var bot = new TownBot(BotKind.Payback, 1);
        if (!ageOnly)
        {
            Main.Driver.RunBot(bot, t => t.Level >= 5, 20 * 60 * 30);
            await Frames(30);
            // The view moved by hand, tilted over the town, as a player leaves it.
            var ext = Main.TownExtent().GetCenter();
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(new Vector3(ext.X + 6, 0, ext.Y + 4), 80f, instant: true);
            await Wait(1500);

            await Strip("pause", () => TestInput.Key(Godot.Key.Escape));
            await Until(() => M.CoverShown, 3000, "the cover");
            M.SettingsButton.GrabFocus();
            await Strip("settings-open", () => TestInput.Key(Godot.Key.Enter), table: true);
            await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 3000, "the settings");
            await Wait(400);
            await TurnStrip("tab-forward", "ribbon.Display");
            await TurnStrip("tab-back", "ribbon.Sound");
            await Strip("settings-close", () => TestInput.Key(Godot.Key.Escape), table: true);
            await Until(() => M.CoverShown, 3000, "the cover");
            M.NewTownButton.GrabFocus();
            await Strip("question", () => TestInput.Key(Godot.Key.Enter), half: 0);
            await Until(() => !M.Motion.Running, 3000, "the question");
            await Strip("question", () => TestInput.Key(Godot.Key.Escape), half: 1);
            await Until(() => M.CoverShown, 3000, "the cover");
            M.ResumeButton.GrabFocus();
            await Strip("resume", () => TestInput.Key(Godot.Key.Enter));
            await Until(() => M.Now == PauseMenu.State.Playing && !M.Motion.Running, 3000, "the town");
            await Wait(400);
            await EscTwiceStrip();
            await Wait(400);
            var hud = Main.Hud;
            await PageStrip("achievements", hud.AchievementsFade, () => hud.OpenAchievements(), () => hud.CloseAchievements());
        }
        // The age-change page: a town at its charter, the next age rung in by its key.
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60);
        Check(Main.Driver.Town.HasCharter, "the bot reached the charter");
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Wait(1500);
        Main.Hud.HoldPageTurnAt = 0f;
        TestInput.Key(Godot.Key.N);
        await Frames(2);
        Check(Main.Hud.PageTurning, "the page turns at the next age");
        for (int k = 0; k < Count; k++)
        {
            Main.Hud.HoldPageTurnAt = TownHud.WipeSeconds * k / (Count - 1);
            await Frames(3);
            await Save("age-page", k);
        }
        Main.Hud.HoldPageTurnAt = -1f;
        // The age page then opens by itself, the same way as the achievements page.
        var age = Main.Hud.AgePageFade;
        age.Motion.Hold(0f);
        await Until(() => Main.Hud.AgePageOpen, 3000, "the age page");
        await PageStrip("age-panel", age, null, () => Main.Hud.CloseAgePage());
        Notes.Add("dir=" + _dir);
    }

    private async Task Wait(int ms)
    {
        ulong t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < (ulong)ms) await Frames(1);
    }

    /// <summary>
    /// Starts a book transition and holds its clock at Count even moments from its start to its end (half 0 or 1: only
    /// the first or second half of the sheet's frames, for a sheet that shows two transitions).
    /// </summary>
    private async Task Strip(string name, Action input, int half = -1, bool table = false)
    {
        M.Motion.Hold(0f);
        Image? before = null;
        if (table && M.Now == PauseMenu.State.Settings) before = await Grab();   // the settings at rest, before Back
        input();
        await Frames(2);
        Check(M.Motion.Running, $"{name}: no transition started");
        if (table && before == null)
        {
            // The table around the book: the frame just before the leaf starts (opening) or just after it has turned
            // back (closing), against the settings at rest; at most 3 of 255 per channel apart.
            M.Motion.Hold(M.LeafStart);
            await Frames(3);
            var edge = await Grab();
            M.Motion.Hold(0f);
            await Frames(3);
            TableCheck(name, edge, null, true);
        }
        int n = half < 0 ? Count : Count / 2, off = half == 1 ? Count / 2 : 0;
        for (int k = 0; k < n; k++)
        {
            M.Motion.Hold(M.Motion.Length * k / (n - 1));
            await Frames(3);
            await Save(name, off + k);
        }
        if (before != null)
        {
            // Closing: the leaf turned back (held there after the frames: holding the clock backwards past the spread's
            // hiding would not show it again).
            M.Motion.Hold(M.LeafEnd);
            await Frames(3);
            TableCheck(name, before, await Grab(), false);
        }
        M.Motion.Release();
        await Until(() => !M.Motion.Running, 3000, name + " to end");
        await Frames(5);
        if (table && name == "settings-open") TableCheck(name, _tableEdge!, await Grab(), true);
    }

    private Image? _tableEdge;

    /// <summary>Round L3: the book closing, Esc again 0.45 s in (the clock running freely so the parts' speeds are
    /// real), and the opening that takes over held at even moments: no part may drop to rest at the takeover.</summary>
    private async Task EscTwiceStrip()
    {
        M.Motion.Release();
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.Motion.Running && M.Motion.Time >= 0.45f, 3000, "the closing under way");
        TestInput.Key(Godot.Key.Escape);
        // Held only once the opening has taken over (holding first would set the closing's clock back before the takeover).
        await Until(() => M.Moving == PauseMenu.Move.Open, 600, "the opening taking over");
        M.Motion.Hold(0f);
        await Frames(2);
        Check(M.Carried, "esc-twice: the opening did not carry the closing's speeds");
        for (int k = 0; k < Count; k++)
        {
            M.Motion.Hold(M.Motion.Length * k / (Count - 1));
            await Frames(3);
            await Save("esc-twice", k);
        }
        M.Motion.Release();
        await Until(() => M.Now == PauseMenu.State.Playing && !M.Motion.Running, 3000, "the town after esc-twice");
    }

    /// <summary>Round L3: the 3D page as the camera shows it at the spread view (before the leaf) against the 2D settings
    /// page drawn over it at rest (the leaf and the pages are drawn in PageLeaf.BookPaper): at most 4 of 255 apart.</summary>
    private void PaperCheck(string name, Image book, Image pages)
    {
        var r = P.PagesRect;
        float half = r.Size.X / 2f, sp = r.Position.X + half, worst = 0f;
        foreach (var (x0, where) in new[] { (sp + half * 0.3f, "right"), (r.Position.X + half * 0.3f, "left") })
        {
            // A strip of the page's foot margin, clear of print on both.
            var patch = new Rect2I((int)x0, (int)(r.End.Y - r.Size.Y * 0.045f), (int)(half * 0.4f), Math.Max(4, (int)(r.Size.Y * 0.025f)));
            var ca = Mean(book, patch); var cb = Mean(pages, patch);
            float d = Math.Max(Math.Abs(ca.R - cb.R), Math.Max(Math.Abs(ca.G - cb.G), Math.Abs(ca.B - cb.B))) * 255f;
            worst = Math.Max(worst, d);
            GD.Print($"STRIPS paper {name} {where} {patch}: 3D page {ca.R * 255:0.0},{ca.G * 255:0.0},{ca.B * 255:0.0}; settings page {cb.R * 255:0.0},{cb.G * 255:0.0},{cb.B * 255:0.0}; differ {d:0.0} of 255");
        }
        Check(worst <= 4f, $"{name}: the settings pages differ from the book's own paper by {worst:0.0} of 255");
    }

    private async Task<Image> Grab()
    {
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return GetViewport().GetTexture().GetImage();
    }

    /// <summary>Opening: called once with the leaf's start (kept), then with the spread at rest. Closing: the spread at rest, then the leaf turned back.</summary>
    private void TableCheck(string name, Image a, Image? b, bool opening)
    {
        if (b == null) { _tableEdge = a; return; }
        if (opening) PaperCheck(name, a, b);
        var r = P.PagesRect;
        var size = a.GetSize();
        // Left of the book (clear of its cover's lip and the page stack), and below it (left of the 3D bookmark).
        var left = new Rect2I(6, (int)(r.Position.Y + r.Size.Y * 0.3f), Math.Max(4, (int)r.Position.X - 50), (int)(r.Size.Y * 0.4f));
        var below = new Rect2I((int)(r.Position.X + r.Size.X * 0.1f), (int)r.End.Y + 40, (int)(r.Size.X * 0.25f), Math.Max(4, size.Y - (int)r.End.Y - 46));
        float worst = 0f;
        foreach (var (patch, where) in new[] { (left, "left"), (below, "below") })
        {
            var ca = Mean(a, patch); var cb = Mean(b, patch);
            float d = Math.Max(Math.Abs(ca.R - cb.R), Math.Max(Math.Abs(ca.G - cb.G), Math.Abs(ca.B - cb.B))) * 255f;
            worst = Math.Max(worst, d);
            GD.Print($"STRIPS table {name} {where} {patch}: {(opening ? "before the leaf" : "settings at rest")} {ca.R * 255:0.0},{ca.G * 255:0.0},{ca.B * 255:0.0}; "
                + $"{(opening ? "settings at rest" : "after the leaf")} {cb.R * 255:0.0},{cb.G * 255:0.0},{cb.B * 255:0.0}; differ {d:0.0} of 255");
        }
        Check(worst <= 3f, $"{name}: the table changes by {worst:0.0} of 255 around the book");
    }

    private static Color Mean(Image img, Rect2I r)
    {
        double R = 0, G = 0, B = 0; int n = 0;
        var size = img.GetSize();
        for (int y = Math.Max(0, r.Position.Y); y < Math.Min(size.Y, r.End.Y); y += 2)
            for (int x = Math.Max(0, r.Position.X); x < Math.Min(size.X, r.End.X); x += 2)
            { var c = img.GetPixel(x, y); R += c.R; G += c.G; B += c.B; n++; }
        return n == 0 ? Colors.Black : new Color((float)(R / n), (float)(G / n), (float)(B / n));
    }

    /// <summary>A page of the interface coming in (half the sheet) and going out (the other half), its clock held.</summary>
    private async Task PageStrip(string name, TownHud.PageFade f, Action? open, Action close)
    {
        f.Motion.Hold(0f);
        open?.Invoke();
        await Frames(2);
        for (int k = 0; k < Count / 2; k++)
        {
            f.Motion.Hold(f.Motion.Length * k / (Count / 2 - 1));
            await Frames(3);
            await Save(name, k);
        }
        f.Motion.Hold(0f);
        close();
        await Frames(2);
        for (int k = 0; k < Count / 2; k++)
        {
            f.Motion.Hold(f.Motion.Length * k / (Count / 2 - 1));
            await Frames(3);
            await Save(name, Count / 2 + k);
        }
        f.Motion.Release();
        await Frames(5);
    }

    private async Task TurnStrip(string name, string ribbon)
    {
        P.HoldTurnAt = 0f;
        var c = P.Controls[ribbon];
        var at = GetViewport().GetFinalTransform() * c.GetGlobalRect().GetCenter();
        TestInput.Motion(at);
        TestInput.Button(at, MouseButton.Left, true);
        TestInput.Button(at, MouseButton.Left, false);
        await Frames(2);
        Check(P.Turning, $"{name}: no page turned");
        for (int k = 0; k < Count; k++)
        {
            P.HoldTurnAt = k / (float)(Count - 1);
            await Frames(3);
            await Save(name, k);
        }
        P.HoldTurnAt = -1f;
        await Until(() => !P.Turning, 3000, name + " to end");
        // Move the pointer off the ribbons so the next sheet does not show a hover.
        TestInput.Motion(new Vector2(960, 1060));
        await Frames(5);
    }

    private async Task Save(string name, int k)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        img.Resize(640, 360, Image.Interpolation.Lanczos);
        img.SavePng(System.IO.Path.Combine(_dir, "frames", $"{name}-{k:00}.png"));
    }
}
